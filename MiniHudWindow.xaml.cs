using System;
using System.Windows;
using System.Windows.Media;
using Netwatch.Services;

namespace Netwatch
{
    public partial class MiniHudWindow : Window
    {
        private readonly ProbeService _probes;

        public MiniHudWindow(ProbeService probes)
        {
            InitializeComponent();
            _probes = probes;
            DataContext = _probes;
            // Place near bottom-right after layout is ready
            Loaded += (_, __) =>
            {
                var work = SystemParameters.WorkArea;
                Left = work.Right - ActualWidth - 16;
                Top = work.Bottom - ActualHeight - 16;
            };
        }

        private void OpenExpanded(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Application.Current is App app)
            {
                app.ShowExpanded();
            }
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void OnHeaderDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void ExitApp(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                this.Hide();
                e.Handled = true;
            }
        }

        private async void RunUploadTest(object sender, RoutedEventArgs e)
        {
            await _probes.RunUploadBloatTestAsync(TimeSpan.FromSeconds(10));
        }
    }
}
