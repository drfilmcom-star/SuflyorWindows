using System;
using System.Windows;
using System.Windows.Input;

namespace SuflyorWindows
{
    public partial class PrompterWindow : Window
    {
        private WindowState oldState;
        private WindowStyle oldStyle;
        private bool fullscreen;

        public PrompterWindow() { InitializeComponent(); }

        public void SetScript(string[] blocks, int index)
        {
            if (blocks == null || blocks.Length == 0) blocks = new[] { "" };
            index = Math.Max(0, Math.Min(index, blocks.Length - 1));
            BlockHeader.Text = "БЛОК " + (index + 1) + " / " + blocks.Length;
            PromptText.Text = blocks[index];
            TextScroller.ScrollToTop();
        }

        public void ResetToBlockStart() => TextScroller.ScrollToTop();

        private void Smaller_Click(object sender, RoutedEventArgs e) => PromptText.FontSize = Math.Max(20, PromptText.FontSize - 4);
        private void Larger_Click(object sender, RoutedEventArgs e) => PromptText.FontSize = Math.Min(120, PromptText.FontSize + 4);

        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (!fullscreen)
            {
                oldState = WindowState; oldStyle = WindowStyle;
                WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized; fullscreen = true;
            }
            else
            {
                WindowState = oldState; WindowStyle = oldStyle; fullscreen = false;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && fullscreen) { WindowState = WindowState.Normal; WindowStyle = WindowStyle.SingleBorderWindow; fullscreen = false; }
            else if (e.Key == Key.Home) ResetToBlockStart();
            else if (e.Key == Key.Add || e.Key == Key.OemPlus) Larger_Click(sender, null!);
            else if (e.Key == Key.Subtract || e.Key == Key.OemMinus) Smaller_Click(sender, null!);
        }
    }
}
