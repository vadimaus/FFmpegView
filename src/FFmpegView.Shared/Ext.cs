using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Text;

namespace FFmpegView
{
    public static class Ext
    {
        public static unsafe AVDictionary* ToHeader(this Dictionary<string, string> headers)
        {
            AVDictionary* options = null;
            StringBuilder builder = new StringBuilder();
            if (headers != null && headers.Count > 0)
            {
                foreach (var header in headers)
                    builder.Append($"{header.Key}: {header.Value}\r\n");
            }
            ffmpeg.av_dict_set(&options, "headers", builder.ToString(), 0);
            return options;
        }

        public static long ToLong(this TimeSpan ts, AVRational timeBase)
        {
            return Convert.ToInt64(ts.TotalSeconds * timeBase.den / timeBase.num); // (secs) * (units) / (secs) = (units)
        }

        public static TimeSpan ToTimeSpan(this long pts, AVRational timeBase)
        {
            return Convert.ToDouble(pts).ToTimeSpan(timeBase);
        }

        public static TimeSpan ToTimeSpan(this double pts, AVRational timeBase)
        {
            if (double.IsNaN(pts) || Math.Abs(pts - ffmpeg.AV_NOPTS_VALUE) <= double.Epsilon)
                return TimeSpan.MinValue;

            return TimeSpan.FromTicks(timeBase.den == 0 ?
                Convert.ToInt64(TimeSpan.TicksPerMillisecond * 1000 * pts / ffmpeg.AV_TIME_BASE) :
                Convert.ToInt64(TimeSpan.TicksPerMillisecond * 1000 * pts * timeBase.num / timeBase.den));
        }
    }
}