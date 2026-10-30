using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SuflyorWindows.Audio;
using SuflyorWindows.Tracking;

namespace SuflyorWindows
{
    public partial class MainWindow : Window
    {
        private PrompterWindow? output;
        private int currentBlock = 0;
        private readonly SherpaSpeechService speech = new();
        private readonly BasicScriptTracker tracker = new();

        public MainWindow()
        {
            InitializeComponent();
            ScriptBox.Text = "[[БЛОК]]\nПервый блок сценария. Вставьте сюда текст.\n\n[[БЛОК]]\nВторой блок сценария.";
            UpdateBlockLabel();
            tracker.SetScript(GetBlocks()[currentBlock]);
            speech.StatusChanged += status => Dispatcher.Invoke(() => SpeechStatus.Text = status);
            speech.TextRecognized += (text, isFinal) => Dispatcher.Invoke(() =>
            {
                RecognizedText.Text = (isFinal ? "Фраза: " : "Сейчас: ") + text;
                if (tracker.AddHypothesis(text, isFinal, out var pos) && output != null && output.IsVisible)
                    output.SetProgress(pos, tracker.Count);
            });
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*" };
            if (dialog.ShowDialog() == true)
            {
                try { ScriptBox.Text = System.IO.File.ReadAllText(dialog.FileName); currentBlock = 0; UpdateBlockLabel(); }
                catch (Exception ex) { MessageBox.Show("Не удалось открыть файл:\n" + ex.Message, "Суфлёр"); }
            }
        }

        private string[] GetBlocks()
        {
            var text = ScriptBox.Text ?? "";
            var blocks = System.Text.RegularExpressions.Regex.Split(text, @"(?im)^\s*\[\[БЛОК\]\]\s*$")
                .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();
            return blocks.Length == 0 ? new[] { text } : blocks;
        }

        private void UpdateBlockLabel()
        {
            var blocks = GetBlocks();
            currentBlock = Math.Max(0, Math.Min(currentBlock, blocks.Length - 1));
            tracker.SetScript(blocks[currentBlock]);
            BlockLabel.Text = (currentBlock + 1) + " / " + blocks.Length;
        }

        private void ScriptBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateBlockLabel();
            if (output != null && output.IsVisible) output.SetScript(GetBlocks(), currentBlock);
        }

        private void ShowOutputButton_Click(object sender, RoutedEventArgs e)
        {
            if (output == null || !output.IsLoaded)
            {
                output = new PrompterWindow();
                output.Closed += (s, args) => output = null;
            }
            output.SetScript(GetBlocks(), currentBlock);
            output.Show();
            output.Activate();
        }

        private void RefreshOutputButton_Click(object sender, RoutedEventArgs e)
        {
            if (output == null || !output.IsVisible) ShowOutputButton_Click(sender, e);
            else output.SetScript(GetBlocks(), currentBlock);
        }

        private async void StartVoiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                tracker.SetScript(GetBlocks()[currentBlock]);
                await speech.StartAsync();
                StartVoiceButton.IsEnabled = false;
                StopVoiceButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Не удалось запустить распознавание", MessageBoxButton.OK, MessageBoxImage.Error);
                SpeechStatus.Text = "Распознавание не запущено";
            }
        }

        private void StopVoiceButton_Click(object sender, RoutedEventArgs e)
        {
            speech.Stop();
            StartVoiceButton.IsEnabled = true;
            StopVoiceButton.IsEnabled = false;
            SpeechStatus.Text = "Распознавание остановлено. Перезапустите приложение для нового сеанса.";
        }

        protected override void OnClosed(EventArgs e)
        {
            speech.Dispose();
            base.OnClosed(e);
        }

        private void ResetBlockButton_Click(object sender, RoutedEventArgs e)
        {
            if (output == null || !output.IsVisible) ShowOutputButton_Click(sender, e);
            currentBlock = Math.Max(0, Math.Min(currentBlock, GetBlocks().Length - 1));
            output!.SetScript(GetBlocks(), currentBlock);
            tracker.Reset(0);
            speech.ResetUtterance();
            output.ResetToBlockStart();
            UpdateBlockLabel();
        }
    }
}
