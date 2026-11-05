#nullable enable
using System;

namespace Puzzle.Client.Layout
{
    /// <summary>
    /// Where a framed painting hangs on a wall: the picture itself, and the height its two wires come down from.
    /// The gilt frame around the picture and the brass plate on the frame's lower bar follow from the picture, by
    /// the measures of the frame's own drawing (<see cref="HallLayout.FrameWidth"/> and the plate's). Lengths are in
    /// points, y growing upwards. No Unity dependency.
    /// </summary>
    public readonly struct FramePlace
    {
        /// <param name="picture">The picture inside the frame.</param>
        /// <param name="wiresFrom">The height the wires hang from: the foot of a picture rail, or the top of the screen.</param>
        /// <exception cref="ArgumentOutOfRangeException">The wires would hang from below the frame.</exception>
        public FramePlace(Box picture, float wiresFrom)
        {
            if (wiresFrom < picture.Top + HallLayout.FrameWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(wiresFrom), wiresFrom, $"A frame whose top is at {picture.Top + HallLayout.FrameWidth} cannot hang from a lower height.");
            }

            Picture = picture;
            WiresFrom = wiresFrom;
        }

        public Box Picture { get; }

        public float WiresFrom { get; }

        /// <summary>The frame's outer edge.</summary>
        public Box Frame => new Box(
            Picture.Left - HallLayout.FrameWidth,
            Picture.Bottom - HallLayout.FrameWidth,
            Picture.Width + 2 * HallLayout.FrameWidth,
            Picture.Height + 2 * HallLayout.FrameWidth);

        /// <summary>The height of the middle of the brass plate under the painting; it is centred under the frame.</summary>
        public float PlateMiddle => Frame.Bottom + HallLayout.PlateOverFrame - HallLayout.PlateHeight / 2;

        /// <summary>The foot of the brass plate: the lowest point of the painting as it hangs.</summary>
        public float PlateBottom => PlateMiddle - HallLayout.PlateHeight / 2;

        /// <summary>The plate is no wider than the frame it hangs on, less its corners.</summary>
        public float WidestPlate => Frame.Width - 2 * HallLayout.FrameWidth;
    }
}
