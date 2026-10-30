using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SuflyorWindows
{
    public partial class MainWindow : Window
    {
        private PrompterWindow? output;
        private int currentBlock = 0;

        public MainWindow()
        {
            InitializeComponent();
            ScriptBox.Text = "[[БЛОК]]\nПервый блок сценария. Вставьте сюда текст.\n\n[[БЛОК]]\nВторой блок сценария.";
            UpdateBlockLabel();
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

        private void ResetBlockButton_Click(object sender, RoutedEventArgs e)
        {
            if (output == null || !output.IsVisible) ShowOutputButton_Click(sender, e);
            currentBlock = Math.Max(0, Math.Min(currentBlock, GetBlocks().Length - 1));
            output!.SetScript(GetBlocks(), currentBlock);
            output.ResetToBlockStart();
            UpdateBlockLabel();
        }
    }
}
