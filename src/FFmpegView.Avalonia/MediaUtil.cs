using System;
using System.Collections.Generic;
using System.Text;

namespace FFmpegView.Avalonia
{
    internal static class MediaUtil
    {
        internal static T Clamp<T>(this T value, T min, T max)
            where T : struct, IComparable
        {
            switch (value)
            {
                case TimeSpan v:
                    {
                        var minT = (TimeSpan)(object)min;
                        var maxT = (TimeSpan)(object)max;

                        if (v.Ticks > maxT.Ticks) return max;
                        if (v.Ticks < minT.Ticks) return min;

                        return value;
                    }

                default:
                    {
                        if (value.CompareTo(min) < 0) return min;
                        return value.CompareTo(max) > 0 ? max : value;
                    }
            }
        }
    }
}
