using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>What a presenter does when several lines play at once.</summary>
    public enum DialogOverlapMode
    {
        /// <summary>Show every playing line (up to a limit, dropping the oldest).</summary>
        Stack,
        /// <summary>Show only the newest line. When it ends, the next newest still playing comes back.</summary>
        Replace,
    }

    /// <summary>The overlap rules the built-in presenters use, for custom presenters to reuse.</summary>
    public static class DialogOverlap
    {
        /// <summary>Picks which playing lines to show.</summary>
        /// <typeparam name="T">Whatever the presenter tracks per line.</typeparam>
        /// <param name="playingOldestFirst">The lines playing now, oldest first.</param>
        /// <param name="mode">Stack or replace.</param>
        /// <param name="maxVisible">For <see cref="DialogOverlapMode.Stack"/>: the most to show, newest kept. 0 or less means no limit.</param>
        /// <param name="result">Cleared, then filled with the lines to show, oldest first.</param>
        public static void SelectVisible<T>(IReadOnlyList<T> playingOldestFirst, DialogOverlapMode mode, int maxVisible, List<T> result)
        {
            result.Clear();
            int count = playingOldestFirst.Count;
            if (count == 0) return;
            int keep = mode == DialogOverlapMode.Replace ? 1 : maxVisible <= 0 ? count : Mathf.Min(maxVisible, count);
            for (int i = count - keep; i < count; i++) result.Add(playingOldestFirst[i]);
        }

        /// <summary>
        /// Moves screen rectangles up until none overlap. Earlier rectangles keep their place; each later one
        /// moves up just above whatever it overlaps. Use it with rectangles in speaking order (oldest first),
        /// so a bubble doesn't jump when someone else starts talking.
        /// </summary>
        /// <param name="rects">Rectangles in screen space (y up). Changed in place.</param>
        /// <param name="spacing">Gap to leave between rectangles that had to move.</param>
        /// <param name="maxY">
        /// The top of the usable screen. A rectangle that would go past it moves below the one it overlaps
        /// instead, so a stack near the top edge stays on screen.
        /// </param>
        public static void SeparateVertically(IList<Rect> rects, float spacing, float maxY = float.PositiveInfinity)
        {
            for (int i = 1; i < rects.Count; i++)
            {
                var r = rects[i];
                // Each pass moves r past one rectangle; repeat until it is clear of all of them.
                for (int pass = 0; pass <= i; pass++)
                {
                    bool moved = false;
                    for (int j = 0; j < i; j++)
                    {
                        var other = rects[j];
                        if (!Overlaps(r, other)) continue;
                        float above = other.yMax + spacing;
                        r.y = above + r.height <= maxY ? above : other.yMin - spacing - r.height;
                        moved = true;
                    }
                    if (!moved) break;
                }
                rects[i] = r;
            }
        }

        static bool Overlaps(Rect a, Rect b) =>
            a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
    }
}
