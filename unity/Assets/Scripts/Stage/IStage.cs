using System;
using Puzzle.Client.Layout;
using UnityEngine;

namespace Puzzle.Client.Stage
{
    /// <summary>
    /// What the player is looking at: the museum's pages, or a level's board. A stage owns the one camera that
    /// draws it and has a clock of its own that only <see cref="Advance"/> moves; the running game feeds it the time
    /// of every frame, the Editor's picture commands whatever moment they want a picture of. Touches reach it as
    /// pixels counted from the screen's lower left corner, as a device reports them.
    /// </summary>
    public interface IStage : IDisposable
    {
        /// <summary>The one camera everything of the stage is drawn by, text included, so its picture is the whole screen.</summary>
        Camera Camera { get; }

        /// <summary>True when nothing on the stage is moving any more. What only twinkles does not count.</summary>
        bool IsSettled { get; }

        /// <summary>Lays the stage out for a screen; call again whenever the screen changes (a fold opens, a bar appears).</summary>
        void Fit(ScreenShape screen);

        /// <summary>Moves the stage's clock on. Nothing on the stage changes with time in any other way.</summary>
        void Advance(float deltaSeconds);

        void PointerDown(Vector2 pixel);

        void PointerMoved(Vector2 pixel);

        void PointerUp();

        /// <summary>The touch was taken away by the device, or the game lost the screen while a finger was down.</summary>
        void PointerCancelled();

        /// <summary>
        /// The player asked for the way back, by the device's back button or gesture: the stage does what its own
        /// way back does (a board asks whether to leave, a jigsaw is left, a painting that hangs forward goes back
        /// to the wall, a page of the museum turns to the hall), or nothing where there is no way back.
        /// </summary>
        void GoBack();
    }

    /// <summary>The camera of a stage: it looks straight at the plane the game is drawn on and shows the screen's points one to one.</summary>
    public static class StageCamera
    {
        /// <summary>The camera looks at the plane the game is drawn on from this far; any distance inside its range shows the same.</summary>
        private const float Distance = 10f;

        /// <summary>What the camera clears to: the wall's own colour, though the wall covers every pixel of it.</summary>
        private static readonly Color WallColour = new Color32(0x17, 0x66, 0x6F, 0xFF);

        public static Camera Add(Transform parent)
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(parent, worldPositionStays: false);
            camera.transform.localPosition = new Vector3(0f, 0f, -Distance);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = WallColour;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            return camera;
        }

        /// <summary>Shows a screen of this many points high, whatever its width.</summary>
        public static void Show(Camera camera, ScreenView view) => camera.orthographicSize = view.Height / 2f;
    }
}
