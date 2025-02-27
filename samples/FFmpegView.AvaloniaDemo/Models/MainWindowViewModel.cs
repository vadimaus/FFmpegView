using Avalonia.Controls;
using FFmpegView.Bass;
using System;
using System.Collections.Generic;
using System.Text;

namespace FFmpegView.AvaloniaDemo.Models
{
    public class MainWindowViewModel: ViewModelBase
    {
        private FFmpegView mediaElement;
        private double? position = 0;
        private double positionStep = 0;
        private TimeSpan? positionTime = TimeSpan.Zero;
        private double playbackStartTime = 0.05;
        private double playbackEndTime = 0.1;
        private bool seekBarVisible = false;

        public FFmpegView? MediaElement
        {
            get
            {
                if (mediaElement == null)
                {
                    FFmpegView? media = App.MainWindow.FindControl<FFmpegView>("Media");
                    mediaElement = media;
                }

                return mediaElement;
            }
        }

        public TimeSpan? PositionTime
        {
            get => positionTime;
            set
            {
                positionTime = value;
                NotifyPropertyChanged(nameof(PositionTime));
            }
        }

        public double? Position
        {
            get => position;
            set
            {
                position = value;
                NotifyPropertyChanged(nameof(Position));
            }
        }

        public double PositionStep
        {
            get => positionStep;
            set
            {
                positionStep = value;
                NotifyPropertyChanged(nameof(PositionStep));
            }
        }

        public double PlaybackStartTime
        {
            get => playbackStartTime;
            set
            {
                playbackStartTime = value;
                NotifyPropertyChanged(nameof(PlaybackStartTime));
            }
        }

        public double PlaybackEndTime
        {
            get => playbackEndTime;
            set
            {
                playbackEndTime = value;
                NotifyPropertyChanged(nameof(PlaybackEndTime));
            }
        }

        public bool SeekBarVisible
        {
            get => seekBarVisible;
            set => SetProperty(ref seekBarVisible, value);
        }
    }
}
