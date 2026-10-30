using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SuflyorWindows.Audio;
using SuflyorWindows.Tracking;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using System.Text.RegularExpressions;

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
            ScriptBox.Text = "Первый блок сценария. Вставьте сюда текст.\n\nВторой блок сценария после пустой строки.\n\n-----\nТретий блок после разделителя.";
            UpdateBlockLabel();
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
            var dialog = new OpenFileDialog { Filter = "Документы Word (*.docx)|*.docx|Текстовые файлы (*.txt)|*.txt" };
            if (dialog.ShowDialog() != true) return;
            try
            {
                string content;
                if (System.IO.Path.GetExtension(dialog.FileName).Equals(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    using var doc = WordprocessingDocument.Open(dialog.FileName, false);
                    var body = doc.MainDocumentPart?.Document.Body;
                    var sb = new StringBuilder();
                    if (body != null)
                    {
                        foreach (var paragraph in body.Elements<Paragraph>())
                        {
                            var line = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
                            sb.AppendLine(line);
                        }
                    }
                    content = sb.ToString();
                }
                else content = System.IO.File.ReadAllText(dialog.FileName, Encoding.UTF8);
                ScriptBox.Text = content;
                currentBlock = 0;
                UpdateBlockLabel();
            }
            catch (Exception ex) { MessageBox.Show("Не удалось открыть документ:\n" + ex.Message, "Суфлёр"); }
        }

        private string[] GetBlocks()
        {
            var text = ScriptBox.Text ?? "";
            var blocks = Regex.Split(text, @"(?m)^\\s*\\[\\[БЛОК\\]\\]\\s*$|(?m)^\\s*-{5,}\\s*$|(?:\\r?\\n)[ \\t]*(?:\\r?\\n)+")
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
            SpeechStatus.Text = "Распознавание остановлено";
        }

        protected override void OnClosed(EventArgs e)
        {
            speech.Dispose();
            base.OnClosed(e);
        }

        private void ResetBlockButton_Click(object sender, RoutedEventArgs e)
        {
            currentBlock = Math.Max(0, Math.Min(currentBlock, GetBlocks().Length - 1));
            tracker.Reset(0);
            speech.ResetUtterance();
            if (output == null || !output.IsVisible) ShowOutputButton_Click(sender, e);
            output!.SetScript(GetBlocks(), currentBlock);
            output.ResetToBlockStart();
            UpdateBlockLabel();
        }
    }
}
