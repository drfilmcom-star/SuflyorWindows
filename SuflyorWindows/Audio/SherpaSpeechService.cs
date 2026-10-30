using NAudio.Wave;
using SherpaOnnx;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace SuflyorWindows.Audio;

/// <summary>Local streaming Russian ASR. Model files are read only from the local Models/asr-ru folder.</summary>
public sealed class SherpaSpeechService : IDisposable
{
    private readonly string _modelDir;
    private OnlineRecognizer? _recognizer;
    private OnlineStream? _stream;
    private WaveInEvent? _microphone;
    private CancellationTokenSource? _cancel;
    private Task? _decodeTask;
    private readonly ConcurrentQueue<float[]> _audio = new();
    private string _lastPartial = "";
    private bool _disposed;

    public event Action<string, bool>? TextRecognized;
    public event Action<string>? StatusChanged;

    public SherpaSpeechService(string? modelDir = null)
    {
        _modelDir = modelDir ?? Path.Combine(AppContext.BaseDirectory, "Models", "asr-ru");
    }

    public bool IsModelAvailable =>
        File.Exists(Path.Combine(_modelDir, "encoder.int8.onnx")) &&
        File.Exists(Path.Combine(_modelDir, "decoder.onnx")) &&
        File.Exists(Path.Combine(_modelDir, "joiner.int8.onnx")) &&
        File.Exists(Path.Combine(_modelDir, "tokens.txt"));

    public async Task StartAsync(int deviceNumber = 0)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SherpaSpeechService));
        if (_microphone != null) return;
        if (!IsModelAvailable)
            throw new FileNotFoundException("Не найдена русская модель sherpa-onnx. Ожидаются encoder.int8.onnx, decoder.onnx, joiner.int8.onnx и tokens.txt в папке Models\\asr-ru.");

        StatusChanged?.Invoke("Загрузка русской модели…");
        await Task.Run(() =>
        {
            var config = new OnlineRecognizerConfig();
            config.FeatConfig.SampleRate = 16000;
            config.FeatConfig.FeatureDim = 80;
            config.ModelConfig.Transducer.Encoder = Path.Combine(_modelDir, "encoder.int8.onnx");
            config.ModelConfig.Transducer.Decoder = Path.Combine(_modelDir, "decoder.onnx");
            config.ModelConfig.Transducer.Joiner = Path.Combine(_modelDir, "joiner.int8.onnx");
            config.ModelConfig.Tokens = Path.Combine(_modelDir, "tokens.txt");
            config.ModelConfig.NumThreads = 2;
            config.ModelConfig.Provider = "cpu";
            config.ModelConfig.ModelType = "zipformer2";
            config.DecodingMethod = "greedy_search";
            config.MaxActivePaths = 4;
            config.EnableEndpoint = 1;
            config.Rule1MinTrailingSilence = 2.0F;
            config.Rule2MinTrailingSilence = 0.8F;
            config.Rule3MinUtteranceLength = 12.0F;
            _recognizer = new OnlineRecognizer(config);
            _stream = _recognizer.CreateStream();
        });

        _cancel = new CancellationTokenSource();
        _decodeTask = Task.Run(() => DecodeLoop(_cancel.Token));
        var devices = WaveIn.DeviceCount;
        if (devices == 0) throw new InvalidOperationException("Windows не обнаружила входных аудиоустройств.");
        var selected = Math.Clamp(deviceNumber, 0, devices - 1);
        _microphone = new WaveInEvent
        {
            DeviceNumber = selected,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 100,
            NumberOfBuffers = 3
        };
        _microphone.DataAvailable += OnAudio;
        _microphone.RecordingStopped += (_, e) =>
        {
            if (e.Exception != null) StatusChanged?.Invoke("Ошибка микрофона: " + e.Exception.Message);
        };
        _microphone.StartRecording();
        StatusChanged?.Invoke("Распознавание активно · sherpa-onnx · CPU · офлайн");
    }

    private void OnAudio(object? sender, WaveInEventArgs e)
    {
        var count = e.BytesRecorded / 2;
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i * 2);
            samples[i] = sample / 32768f;
        }
        _audio.Enqueue(samples);
    }

    private void DecodeLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (!_audio.TryDequeue(out var samples))
            {
                Thread.Sleep(10);
                continue;
            }
            try
            {
                var stream = _stream!;
                var recognizer = _recognizer!;
                stream.AcceptWaveform(16000, samples);
                while (recognizer.IsReady(stream)) recognizer.Decode(stream);
                var text = recognizer.GetResult(stream).Text?.Trim() ?? "";
                var endpoint = recognizer.IsEndpoint(stream);
                if (text.Length > 0 && text != _lastPartial)
                {
                    _lastPartial = text;
                    TextRecognized?.Invoke(text, endpoint);
                }
                if (endpoint)
                {
                    recognizer.Reset(stream);
                    _lastPartial = "";
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("Ошибка распознавания: " + ex.Message);
            }
        }
    }

    public void ResetUtterance()
    {
        while (_audio.TryDequeue(out _)) { }
        if (_recognizer != null && _stream != null) _recognizer.Reset(_stream);
        _lastPartial = "";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _microphone?.StopRecording();
        _microphone?.Dispose();
        _microphone = null;
        _cancel?.Cancel();
        try { _decodeTask?.Wait(1500); } catch { }
        _stream?.Dispose();
        _recognizer?.Dispose();
        _cancel?.Dispose();
    }
}
