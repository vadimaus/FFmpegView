using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FFmpegView.Avalonia;
using FFmpegView.Bass;
using System;
using System.IO;

namespace FFmpegView.AvaloniaDemo
{
    public partial class MainWindow : Window
    {
        private FFmpegView Media;
        private string source;

        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);
            InitializeComponent();
            DataContext = App.ViewModel;

            var media = this.FindControl<FFmpegView>("Media");
            media.SetAudioHandler(new BassAudioStreamDecoder());
            media.PositionChanged += OnMediaPositionChanged;
        }

        private void InitializeComponent()
        {
            Width = 800;
            Height = 600;
        }

        private void OnMediaPositionChanged(object sender, PositionChangedEventArgs e)
        {
            var media = sender as FFmpegView;

            if (!media.IsOpen)
            {
                return;
            }

            if (media.State == MediaState.IsSeeking)
                return;

            App.ViewModel.PositionTime = media.Position;
            App.ViewModel.Position = media.Position?.TotalSeconds;

            App.ViewModel.PlaybackStartTime = media.PlaybackStartTime?.TotalSeconds ?? 0;
            App.ViewModel.PlaybackEndTime = media.PlaybackEndTime?.TotalSeconds ?? 0;
        }

        private void OnPlayClick(object? sender, RoutedEventArgs e)
        {
            if (Design.IsDesignMode)
                return;

           
            App.ViewModel?.MediaElement?.Play();
        }

        private async void OnPauseClick(object sender, RoutedEventArgs e)
        {
            if (Design.IsDesignMode)
                return;

            App.ViewModel?.MediaElement?.Pause();
        }

        private void OnStopClick(object sender, RoutedEventArgs e)
        {
            if (Design.IsDesignMode)
                return;

            App.ViewModel.MediaElement.Position = TimeSpan.Zero;
        }

        private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
        {
            if (Design.IsDesignMode)
                return;

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.AllowMultiple = false;

            var result = await openFileDialog.ShowAsync(this);

            if (result != null)
            {
                foreach (string filePath in result)
                {
                    if (File.Exists(filePath))
                    {
                        source = filePath;

                        App.ViewModel?.MediaElement?.Play(filePath);
                        System.Threading.Thread.Sleep(10);
                        App.ViewModel?.MediaElement?.Pause();
                    }
                }
            }
        }
    }
}