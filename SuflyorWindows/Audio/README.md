# Локальное распознавание речи

Сервис использует sherpa-onnx C# API и NAudio. Микрофон захватывается локально в PCM mono 16 kHz; декодирование выполняется в отдельном рабочем потоке.

Ожидаемая папка модели: `Models/asr-ru`
- `encoder.int8.onnx`
- `decoder.onnx`
- `joiner.int8.onnx`
- `tokens.txt`

Параметры совпадают с русской веткой Android Suflyor: 16 kHz, 80 mel bins, Zipformer2, CPU, 2 потока, endpoint после 0.8 секунды тишины.

Важно: текущий BasicScriptTracker является промежуточной Windows-реализацией. Полный перенос ScriptTracker/TextNorm из Android и проверка позиционирования на реальных сценариях остаются обязательными перед релизом.
