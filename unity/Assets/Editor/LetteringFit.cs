#nullable enable
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// What a picture of the tools says of its lettering's fit, in any language: a line that runs past an edge of the
    /// screen is cut, and is told of in the log as the picture is taken, with its words and how far it runs over. A
    /// line wholly out of sight (a card that waits above the screen) is no line of the picture. How much smaller than
    /// its size a line had to be set to fit its room the game's lettering tells itself (Lettering.SayWithin).
    /// </summary>
    internal static class LetteringFit
    {
        /// <summary>A line may run this far past an edge, in points: the soft rim of a letter's outline.</summary>
        private const float Slack = 0.5f;

        /// <summary>How many lines were found cut since the tools began: the commands that take pictures report it at their end.</summary>
        public static int CutLines { get; private set; }

        /// <summary>Tells of every line of the scene that the camera's view cuts.</summary>
        public static void Check(Camera camera)
        {
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Vector3 middle = camera.transform.position;
            var view = new Rect(middle.x - halfWidth, middle.y - halfHeight, 2f * halfWidth, 2f * halfHeight);
            foreach (TextMeshPro line in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Exclude))
            {
                if (!line.enabled || string.IsNullOrEmpty(line.text) || line.textInfo.characterCount == 0)
                {
                    continue;
                }

                Bounds drawn = line.renderer.bounds;
                bool isInSight = drawn.max.x > view.xMin && drawn.min.x < view.xMax && drawn.max.y > view.yMin && drawn.min.y < view.yMax;
                if (!isInSight)
                {
                    continue;
                }

                var over = new List<string>();
                AddWhereItRunsOver(over, "left", view.xMin - drawn.min.x);
                AddWhereItRunsOver(over, "right", drawn.max.x - view.xMax);
                AddWhereItRunsOver(over, "top", drawn.max.y - view.yMax);
                AddWhereItRunsOver(over, "bottom", view.yMin - drawn.min.y);
                if (over.Count > 0)
                {
                    CutLines++;
                    Debug.LogWarning($"Lettering fit: \"{line.text.Replace("\n", " / ")}\" ({PathOf(line.transform)}) runs past the screen's {string.Join(", ", over)}.");
                }
            }
        }

        private static void AddWhereItRunsOver(List<string> over, string edge, float by)
        {
            if (by > Slack)
            {
                over.Add($"{edge} edge by {by:0.#} points");
            }
        }

        private static string PathOf(Transform part)
        {
            string path = part.name;
            for (Transform? up = part.parent; up != null; up = up.parent)
            {
                path = $"{up.name}/{path}";
            }

            return path;
        }
    }
}
