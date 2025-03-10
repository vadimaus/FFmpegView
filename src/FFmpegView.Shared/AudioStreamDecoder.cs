﻿using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static FFmpegView.IMedia;

namespace FFmpegView
{
    public unsafe abstract class AudioStreamDecoder : IMedia
    {
        int error;
        byte* bufferPtr;
        IntPtr audioBuffer;
        AVFormatContext* format;
        AVCodecContext* codecContext;
        AVStream* audioStream;
        AVPacket* packet;
        AVFrame* frame;
        SwrContext* convert;
        int audioStreamIndex;
        bool isNextFrame = true;
        TimeSpan lastTime;
        TimeSpan OffsetClock;
        readonly object syncLock = new object();
        readonly Stopwatch clock = new Stopwatch();
        public MediaState State { get; private set; }
        public TimeSpan FrameDuration { get; private set; }
        public TimeSpan Duration { get; private set; }
        public TimeSpan Position => OffsetClock + clock.Elapsed;
        public string CodecName { get; private set; }
        public string CodecId { get; private set; }
        public int Channels { get; private set; }
        public AVSampleFormat SampleFormat { get; private set; }
        public long Bitrate { get; protected set; }
        public int SampleRate { get; protected set; }
        public long BitsPerSample { get; protected set; }
        public bool IsPlaying { get; protected set; }
        public Dictionary<string, string> Headers { get; set; }
        public event MediaHandler MediaCompleted;
        public event MediaMsgHandler MediaMsgRecevice;

        public AudioStreamDecoder()
        {
            Headers = new Dictionary<string, string>();
        }

        public bool InitDecodecAudio(string path)
        {
            try
            {
                format = ffmpeg.avformat_alloc_context();
                var tempFormat = format;
                AVDictionary* options = Headers.ToHeader();
                error = ffmpeg.avformat_open_input(&tempFormat, path, null, &options);

                if (error < 0)
                {
                    SendMsg(MsgType.Information, "Failed to open media file");
                    return false;
                }

                ffmpeg.avformat_find_stream_info(format, null);
                AVCodec* codec;
                audioStreamIndex = ffmpeg.av_find_best_stream(format, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, &codec, 0);

                if (audioStreamIndex < 0)
                {
                    SendMsg(MsgType.Information, "No audio stream found");
                    return false;
                }

                audioStream = format->streams[audioStreamIndex];
                codecContext = ffmpeg.avcodec_alloc_context3(codec);
                error = ffmpeg.avcodec_parameters_to_context(codecContext, audioStream->codecpar);

                if (error < 0)
                    SendMsg(MsgType.Information, "Setting decoder parameters failed");

                error = ffmpeg.avcodec_open2(codecContext, codec, null);
                Duration = TimeSpan.FromMilliseconds(format->duration / 1000);
                CodecId = codec->id.ToString();
                CodecName = ffmpeg.avcodec_get_name(codec->id);
                Bitrate = codecContext->bit_rate;
                long channelLayout = unchecked((long)codecContext->channel_layout);
                Channels = codecContext->channels;
                SampleRate = codecContext->sample_rate;
                SampleFormat = codecContext->sample_fmt;
                BitsPerSample = ffmpeg.av_samples_get_buffer_size(null, 2, codecContext->frame_size, AVSampleFormat.AV_SAMPLE_FMT_S16, 1);
                audioBuffer = Marshal.AllocHGlobal((int)BitsPerSample);
                bufferPtr = (byte*)audioBuffer;

                InitConvert(channelLayout, AVSampleFormat.AV_SAMPLE_FMT_S16, SampleRate, channelLayout, SampleFormat, SampleRate);

                packet = ffmpeg.av_packet_alloc();
                frame = ffmpeg.av_frame_alloc();
                State = MediaState.Read;
                return true;
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Error, ex.Message);
            }
            return false;
        }

        bool InitConvert(long occ, AVSampleFormat osf, int osr, long icc, AVSampleFormat isf, int isr)
        {
            try
            {
                convert = ffmpeg.swr_alloc();
                var tempConvert = convert;
                ffmpeg.swr_alloc_set_opts(tempConvert, occ, osf, osr, icc, isf, isr, 0, null);

                if (convert == null)
                    return false;

                ffmpeg.swr_init(convert);
                return true;
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Error, ex.Message);
            }
            return false;
        }

        bool TryReadNextFrame(out AVFrame outFrame)
        {
            try
            {
                if (lastTime == TimeSpan.Zero)
                {
                    lastTime = Position;
                    isNextFrame = true;
                }
                else
                {
                    if (Position - lastTime >= FrameDuration)
                    {
                        lastTime = Position;
                        isNextFrame = true;
                    }
                    else
                    {
                        outFrame = *frame;
                        return false;
                    }
                }
                if (isNextFrame && error >= 0)
                {
                    lock (syncLock)
                    {
                        int result = -1;
                        ffmpeg.av_frame_unref(frame);

                        while (true)
                        {
                            result = ffmpeg.av_read_frame(format, packet);
                            if (result == ffmpeg.AVERROR_EOF || result < 0)
                            {
                                outFrame = *frame;
                                StopPlay();
                                return false;
                            }

                            if (packet->stream_index != audioStreamIndex)
                                continue;

                            ffmpeg.avcodec_send_packet(codecContext, packet);
                            result = ffmpeg.avcodec_receive_frame(codecContext, frame);

                            if (result < 0) continue;

                            FrameDuration = TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * 1000d * frame->nb_samples / frame->sample_rate, 0));
                            outFrame = *frame;
                            ffmpeg.av_packet_unref(packet);

                            return true;
                        }
                    }
                }
                else
                {
                    outFrame = *frame;
                    return false;
                }
            }
            catch (Exception ex)
            {
                outFrame = default;
                SendMsg(MsgType.Error, ex.Message);
                return false;
            }
        }
        bool StopPlay()
        {
            try
            {
                lock (syncLock)
                {
                    if (State == MediaState.None)
                        return false;

                    IsPlaying = false;
                    OffsetClock = TimeSpan.FromSeconds(0);

                    clock.Reset();
                    clock.Stop();

                    var tempFormat = format;
                    ffmpeg.avformat_free_context(tempFormat);
                    format = null;
                    var tempCodecContext = codecContext;
                    ffmpeg.avcodec_free_context(&tempCodecContext);
                    var tempPacket = packet;
                    ffmpeg.av_packet_free(&tempPacket);
                    var tempFrame = frame;
                    ffmpeg.av_frame_free(&tempFrame);
                    var tempConvert = convert;
                    ffmpeg.swr_free(&tempConvert);
                    Marshal.FreeHGlobal(audioBuffer);
                    bufferPtr = null;
                    audioStream = null;
                    error = -1;
                    audioStreamIndex = -1;
                    //Duration = TimeSpan.FromMilliseconds(0);
                    CodecName = string.Empty;
                    CodecId = string.Empty;
                    Channels = 0;
                    Bitrate = 0;
                    SampleRate = 0;
                    BitsPerSample = 0;
                    State = MediaState.None;
                    lastTime = TimeSpan.Zero;
                    Invoke(Duration);
                }

                return true;
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Error, ex.Message);
            }

            return false;
        }

        public bool SeekProgress(TimeSpan seekTime)
        {
            try
            {
                if (format == null || error >= 0)
                    return false;

                lock (syncLock)
                {
                    clock.Stop();
                    clock.Reset();
                    State = MediaState.IsSeeking;
                    IsPlaying = false;

                    var timeBase = audioStream->time_base;
                    var timestamp = seekTime.ToLong(timeBase);

                    //seekTime / ffmpeg.av_q2d(audioStream->time_base);
                    ffmpeg.av_seek_frame(format, audioStreamIndex, timestamp, ffmpeg.AVSEEK_FLAG_BACKWARD | ffmpeg.AVSEEK_FLAG_FRAME);
                    ffmpeg.av_frame_unref(frame);
                    ffmpeg.av_packet_unref(packet);
                    int error = 0;

                    while (packet->pts < timestamp)
                    {
                        do
                        {
                            do
                            {
                                ffmpeg.av_packet_unref(packet);
                                error = ffmpeg.av_read_frame(format, packet);
                                if (error == ffmpeg.AVERROR_EOF)
                                    return false;
                            }
                            while (packet->stream_index != audioStreamIndex);

                            ffmpeg.avcodec_send_packet(codecContext, packet);
                            error = ffmpeg.avcodec_receive_frame(codecContext, frame);
                           
                        }
                        while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));
                    }

                    OffsetClock = seekTime;
                    lastTime = TimeSpan.Zero;
                }

                return true;
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Error, ex.Message);
            }
            return false;
        }
        byte[] FrameConvertBytes(AVFrame* sourceFrame)
        {
            try
            {
                var tempBufferPtr = bufferPtr;
                var outputSamplesPerChannel = ffmpeg.swr_convert(convert, &tempBufferPtr, frame->nb_samples, sourceFrame->extended_data, sourceFrame->nb_samples);
                var outPutBufferLength = ffmpeg.av_samples_get_buffer_size(null, 2, outputSamplesPerChannel, AVSampleFormat.AV_SAMPLE_FMT_S16, 1);
                if (outputSamplesPerChannel < 0)
                    return null;

                byte[] bytes = new byte[outPutBufferLength];
                Marshal.Copy(audioBuffer, bytes, 0, bytes.Length);
                return bytes;
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Error, ex.Message);
            }
            return null;
        }

        public bool Play()
        {
            try
            {
                if (State == MediaState.Play)
                    return false;

                clock.Start();
                IsPlaying = true;
                State = MediaState.Play;
                return true;
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Error, ex.Message);
            }
            return false;
        }

        public bool Pause()
        {
            try
            {
                PauseCore();

                if (State != MediaState.Play)
                    return false;

                clock.Stop();
                IsPlaying = false;
                //OffsetClock = clock.Elapsed;
                //clock.Reset();
                State = MediaState.Pause;

                return true;
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Error, ex.Message);
            }
            return false;
        }
        public bool Stop()
        {
            if (State == MediaState.None)
                return false;
            return StopPlay();
        }
        public bool TryPlayNextFrame()
        {
            if (TryReadNextFrame(out var frame))
            {
                var bytes = FrameConvertBytes(&frame);
                if (bytes != null)
                {
                    PlayNextFrame(bytes);
                    return true;
                }
            }
            return false;
        }
        public abstract void Prepare();
        public abstract void StopCore();
        public abstract void PauseCore();
        public abstract void PlayNextFrame(byte[] bytes);
        protected void Invoke(TimeSpan duration) => MediaCompleted?.Invoke(duration);
        protected void SendMsg(MsgType type, string msg) => MediaMsgRecevice?.Invoke(type, msg);
    }
}