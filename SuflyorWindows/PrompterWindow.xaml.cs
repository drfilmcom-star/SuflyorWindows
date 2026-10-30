using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SuflyorWindows
{
    public partial class PrompterWindow : Window
    {
        private WindowState oldState;
        private WindowStyle oldStyle;
        private bool fullscreen;
        private bool scrolling = true;
        private double targetOffset;
        private readonly DispatcherTimer scrollTimer;

        public PrompterWindow()
        {
            InitializeComponent();
            scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            scrollTimer.Tick += (_, _) =>
            {
                if (!scrolling) return;
                var current = TextScroller.VerticalOffset;
                var distance = targetOffset - current;
                if (Math.Abs(distance) < 0.5) return;
                TextScroller.ScrollToVerticalOffset(current + distance * 0.12 * SpeedSlider.Value);
            };
            scrollTimer.Start();
        }

        public void SetScript(string[] blocks, int index)
        {
            if (blocks == null || blocks.Length == 0) blocks = new[] { "" };
            index = Math.Max(0, Math.Min(index, blocks.Length - 1));
            BlockHeader.Text = "БЛОК " + (index + 1) + " / " + blocks.Length;
            PromptText.Text = blocks[index];
            TextScroller.ScrollToTop();
            targetOffset = 0;
        }

        public void ResetToBlockStart()
        {
            targetOffset = 0;
            TextScroller.ScrollToTop();
        }

        public void SetProgress(int position, int total)
        {
            if (total <= 0) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var ratio = Math.Clamp((double)position / total, 0.0, 1.0);
                targetOffset = TextScroller.ScrollableHeight * ratio;
            }));
        }

        private void Smaller_Click(object sender, RoutedEventArgs e) => PromptText.FontSize = Math.Max(20, PromptText.FontSize - 4);
        private void Larger_Click(object sender, RoutedEventArgs e) => PromptText.FontSize = Math.Min(120, PromptText.FontSize + 4);
        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { }

        private void StartScroll_Click(object sender, RoutedEventArgs e)
        {
            scrolling = true;
            StartScrollButton.IsEnabled = false;
            StopScrollButton.IsEnabled = true;
        }

        private void StopScroll_Click(object sender, RoutedEventArgs e)
        {
            scrolling = false;
            StartScrollButton.IsEnabled = true;
            StopScrollButton.IsEnabled = false;
        }

        private void MirrorCheck_Changed(object sender, RoutedEventArgs e) => TextMirror.ScaleX = MirrorCheck.IsChecked == true ? -1 : 1;

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
            else if (e.Key == Key.Space) { if (scrolling) StopScroll_Click(sender, null!); else StartScroll_Click(sender, null!); }
        }

        protected override void OnClosed(EventArgs e)
        {
            scrollTimer.Stop();
            base.OnClosed(e);
        }
    }
}
