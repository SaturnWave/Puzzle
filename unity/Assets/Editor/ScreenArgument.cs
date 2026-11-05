#nullable enable
using System;
using Puzzle.Client.Layout;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// A screen as the Editor's picture and check commands are told of one (<see cref="ScreenShape.Parse"/>): 1080x2340,
    /// 1080x2340+96+48 with its top and bottom strips, 2152x2076+0+0+90+0 with its side strips too, as a device held
    /// sideways may have them.
    /// </summary>
    internal static class ScreenArgument
    {
        /// <exception cref="ArgumentException">The text names no screen, or one whose strips leave nothing of it.</exception>
        public static ScreenShape Of(string text)
        {
            try
            {
                return ScreenShape.Parse(text);
            }
            catch (FormatException failure)
            {
                throw new ArgumentException(failure.Message, failure);
            }
        }
    }
}
