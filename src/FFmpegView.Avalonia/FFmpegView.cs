﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Logging;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using FFmpegView.Avalonia;
using PCLUntils.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FFmpegView
{
    [PseudoClasses(":empty")]
    [TemplatePart("PART_ImageView", typeof(Image))]
    public unsafe class FFmpegView : TemplatedControl, IFFmpegView
    {
        private Image image;
        private Task playTask;
        private Task audioTask;
        private Bitmap bitmap;
        private bool _isAttached = false;
        private bool _isRunning = true;
        private readonly bool isInit = false;
        private AudioStreamDecoder audio;
        private readonly TimeSpan timeout;
        private readonly VideoStreamDecoder video;
        private CancellationTokenSource cancellationToken;
        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<FFmpegView, Stretch>(nameof(Stretch), Stretch.Uniform);


        private bool isOpen;
        private bool isPlaying;
        private bool isSeeking;
        private bool hasMediaEnded;
        private bool isStopped;

        public bool IsOpen
        {
            get => isOpen;
            private set
            {
                isOpen = value;
                OnPropertyChanged(nameof(IsOpen));
            }
        }

        public bool IsPlaying
        {
            get => isPlaying;
            set
            {
                isPlaying = value;
                OnPropertyChanged(nameof(IsPlaying));
            }
        }

        public bool IsSeeking
        {
            get => isSeeking;
            set
            {
                isSeeking = value;
                OnPropertyChanged(nameof(IsSeeking));
            }
        }

        public bool HasMediaEnded
        {
            get => hasMediaEnded;
            set
            {
                hasMediaEnded = value;
                OnPropertyChanged(nameof(HasMediaEnded));
            }
        }

        public bool IsStopped
        {
            get => isStopped;
            set
            {
                isStopped = value;
                OnPropertyChanged(nameof(IsStopped));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<PositionChangedEventArgs> PositionChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Uri Source { get; private set; }

        public void UpdateSource(Uri newSource) => Source = newSource;

        public TimeSpan? PlaybackStartTime => video?.StartTime;

        public TimeSpan? PlaybackEndTime => video?.Duration;

        public MediaState State => video.State;

        /// <summary>
        /// Gets or sets a value controlling how the video will be stretched.
        /// </summary>
        public Stretch Stretch
        {
            get => GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }
        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            try
            {
                cancellationToken.Cancel();
                playTask.Dispose();
                audioTask.Dispose();
            }
            catch (Exception ex)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
            }
        }
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isAttached = true;
            base.OnAttachedToVisualTree(e);
        }
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isAttached = false;
            base.OnDetachedFromVisualTree(e);
        }
        static FFmpegView()
        {
            StretchProperty.Changed.AddClassHandler<FFmpegView>(OnStretchChange);
        }

        public void SetAudioHandler(AudioStreamDecoder decoder) => audio = decoder;
        public void SetHeader(Dictionary<string, string> headers) => video.Headers = headers;
        private static void OnStretchChange(FFmpegView sender, AvaloniaPropertyChangedEventArgs e)
        {
            try
            {
                if (e.NewValue is Stretch stretch)
                    sender.image.Stretch = stretch;
            }
            catch { }
        }
        public FFmpegView()
        {
            video = new VideoStreamDecoder();
            video.Headers = new Dictionary<string, string> { { "User-Agent", "ffmpeg_demo" } };

            //audio = new BassAudioStreamDecoder();
            //audio.Headers = new Dictionary<string, string> { { "User-Agent", "ffmpeg_demo" } };

            timeout = TimeSpan.FromTicks(10000);
            video.MediaCompleted += VideoMediaCompleted;
            video.MediaMsgRecevice += Video_MediaMsgRecevice;
            isInit = Init();
        }
        private void Video_MediaMsgRecevice(MsgType type, string msg)
        {
            if (type == MsgType.Error)
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, msg);
            else
                Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, msg);
        }
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            image = e.NameScope.Get<Image>("PART_ImageView");
        }
        private void VideoMediaCompleted(TimeSpan duration) =>
                    Dispatcher.UIThread.InvokeAsync(DisplayVideoInfo);

        public static readonly StyledProperty<TimeSpan> PositionProperty = AvaloniaProperty.Register<FFmpegView, TimeSpan>(
          nameof(Position), TimeSpan.Zero, false, BindingMode.TwoWay, null, (o, v) => OnPositionPropertyChanging(o, v), null);

        public TimeSpan? Position
        {
            get => (TimeSpan?)GetValue(PositionProperty);
            set
            {
                SetValue(PositionProperty, value);
            }
        }

        private void ReportPlaybackPosition() => ReportPlaybackPosition(video.Position);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReportPlaybackPosition(TimeSpan newPosition)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var oldPosition = Position;
                if (oldPosition?.Ticks == newPosition.Ticks || (newPosition.TotalSeconds > 0 && newPosition.TotalSeconds >= PlaybackEndTime?.TotalSeconds))
                    return;

                Position = newPosition;
                PositionChanged?.Invoke(this, new PositionChangedEventArgs(oldPosition ?? default, newPosition));
            });
        }

        private static TimeSpan OnPositionPropertyChanging(IAvaloniaObject d, TimeSpan value)
        {
            if (d == null || d is FFmpegView == false) return value;

            var element = (FFmpegView)d;

            if (!element.IsOpen)
                return TimeSpan.Zero;

            if (!element.IsSeeking)
                return value;

            var targetSeek = (TimeSpan)value;
            var minTarget = element.PlaybackStartTime ?? TimeSpan.Zero;
            var maxTarget = element.PlaybackEndTime ?? TimeSpan.Zero;
            var hasValidTaget = maxTarget > minTarget;

            if (hasValidTaget)
            {
                targetSeek = targetSeek.Clamp(minTarget, maxTarget);

                element?.Pause();
                element?.SeekTo(targetSeek);
            }
            else
            {
                targetSeek = element.Position.Value;
            }

            return targetSeek;
        }

        public bool Play()
        {
            bool isPlaying = false;

            try
            {
                isPlaying = IsPlaying = video.Play();
                //IsSeeking = false;
                IsStopped = false;
                HasMediaEnded = false;
                audio?.Play();
            }
            catch (Exception ex)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
            }

            return isPlaying;
        }

        public bool Play(string uri, Dictionary<string, string> headers = null)
        {
            bool isPlaying = false;

            if (!isInit)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "FFmpeg : dosnot initialize device");
                return false;
            }

            try
            {
                if (video.State == MediaState.None)
                {
                    video.Headers = headers;
                    video.InitDecodecVideo(uri);
                    audio?.InitDecodecAudio(uri);
                    audio?.Prepare();
                    DisplayVideoInfo();

                    UpdateSource(new Uri(uri));
                    IsOpen = video != null ? video.IsInitialized : false;
                }

                IsPlaying = isPlaying = video.Play();
                IsStopped = false;
                audio?.Play();
            }
            catch (Exception ex)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
            }

            return isPlaying;
        }

        public bool SeekTo(TimeSpan seekTime)
        {
            try
            {
                _ = audio?.SeekProgress(seekTime);
                return video.SeekProgress(seekTime);
            }
            catch (Exception ex)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
                return false;
            }
        }

        public bool Pause()
        {
            try
            {
                audio?.Pause();
                bool isPaused = video.Pause();

                IsPlaying = false;
                IsStopped = false;

                return isPaused;
            }
            catch (Exception ex)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
                return false;
            }
        }
        public bool Stop()
        {
            try
            {
                audio?.Stop();
                bool isStopped = video.Stop();
                IsPlaying = false;
                IsSeeking = false;
                ReportPlaybackPosition();
                return true;
            }
            catch (Exception ex)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
                return false;
            }
        }
        bool Init()
        {
            try
            {
                cancellationToken = new CancellationTokenSource();
                playTask = new Task(DrawImage, cancellationToken.Token);
                playTask.Start();
                audioTask = new Task(() =>
                {
                    while (_isRunning)
                    {
                        try
                        {
                            if (audio?.IsPlaying == true)
                            {
                                if (audio?.TryPlayNextFrame() == true)
                                {
                                    Thread.Sleep(audio.FrameDuration.Subtract(timeout));
                                    ReportPlaybackPosition();
                                }
                            }
                            else
                                Thread.Sleep(10);
                        }
                        catch (Exception ex)
                        {
                            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
                        }
                    }
                }, cancellationToken.Token);

                audioTask.Start();

                return true;
            }
            catch (Exception ex)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "FFmpeg Failed Init: " + ex.Message);
                return false;
            }
        }


#if NET40_OR_GREATER
        [SecurityCritical]
        [HandleProcessCorruptedStateExceptions]
#endif

        private void DrawImage()
        {
            while (_isRunning)
            {
                try
                {
                    if (video.IsPlaying && _isAttached)
                    {
                        if (video.TryReadNextFrame(out var frame))
                        {
                            var convertedFrame = video.FrameConvert(&frame);
                            bitmap?.Dispose();
                            bitmap = new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, (IntPtr)convertedFrame.data[0], new PixelSize(video.FrameWidth, video.FrameHeight), new Vector(96, 96), convertedFrame.linesize[0]);

                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                if (image.IsNotEmpty())
                                    image.Source = bitmap;
                            });

                            ReportPlaybackPosition();
                            Thread.Sleep(video.FrameDuration.Subtract(timeout));
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
                }
            }
        }

        #region 视频信息
        private string codec;
        public string Codec => codec;
        private TimeSpan duration;
        public TimeSpan Duration => duration;
        private double videoFps;
        public double VideoFps => videoFps;
        private double frameHeight;
        public double FrameHeight => frameHeight;
        private double frameWidth;
        public double FrameWidth => frameWidth;
        private int videoBitrate;
        public int VideoBitrate => videoBitrate;
        private double sampleRate;
        public double SampleRate => sampleRate;
        private long audioBitrate;
        public long AudioBitrate => audioBitrate;
        private long audioBitsPerSample;
        public long AudioBitsPerSample => audioBitsPerSample;
        void DisplayVideoInfo()
        {
            try
            {
                duration = video.Duration;
                codec = video.CodecName;
                videoBitrate = video.Bitrate;
                frameWidth = video.FrameWidth;
                frameHeight = video.FrameHeight;
                videoFps = video.FrameRate;
                if (audio != null)
                {
                    audioBitrate = audio.Bitrate;
                    sampleRate = audio.SampleRate;
                    audioBitsPerSample = audio.BitsPerSample;
                }
            }
            catch { }
        }
        #endregion
    }
}