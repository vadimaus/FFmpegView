using System;
using System.Collections.Generic;
using System.Text;

namespace FFmpegView.Avalonia
{
    /// <summary>
    /// Contains the position changed routed event args.
    /// </summary>
    /// <seealso cref="EventArgs" />
    public class PositionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PositionChangedEventArgs" /> class.
        /// </summary>
        /// <param name="oldPosition">The old position.</param>
        /// <param name="newPosition">The new position.</param>
        internal PositionChangedEventArgs(TimeSpan oldPosition, TimeSpan newPosition)
        {
            Position = newPosition;
            OldPosition = oldPosition;
        }

        /// <summary>
        /// Gets the current position.
        /// </summary>
        public TimeSpan Position { get; }

        /// <summary>
        /// Gets the old position.
        /// </summary>
        public TimeSpan OldPosition { get; }
    }
}
