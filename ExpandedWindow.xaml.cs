using System;
using System.Windows;
using System.Windows.Input;
using Netwatch.Services;

namespace Netwatch
{
    public partial class ExpandedWindow : Window
    {
        private readonly ProbeService _probes;
        public ExpandedWindow(ProbeService probes)
        {
            InitializeComponent();
            _probes = probes;
            DataContext = _probes;
        }

        private async void RunUploadTest(object sender, RoutedEventArgs e)
        {
            await _probes.RunUploadBloatTestAsync(TimeSpan.FromSeconds(10));
        }

        private void CopySnapshot(object sender, RoutedEventArgs e)
        {
            try { System.Windows.Clipboard.SetText(_probes.BuildSnapshot()); } catch { }
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void CloseExpanded(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnHeaderDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }
    }
}
