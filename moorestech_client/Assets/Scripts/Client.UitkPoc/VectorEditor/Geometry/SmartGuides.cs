using System.Collections.Generic;
using UnityEngine;

namespace Client.UitkPoc.VectorEditor.Geometry
{
    // 動かしている矩形の左・中・右／上・中・下を、他の図形の同じ線へ吸着させ、合った線をガイドとして返す
    // Snaps the moving box's left/center/right and top/middle/bottom to other shapes' lines and returns the matched guides
    public static class SmartGuides
    {
        public readonly struct Result
        {
            public readonly Vector2 Offset;
            public readonly float? GuideX;
            public readonly float? GuideY;

            public Result(Vector2 offset, float? guideX, float? guideY)
            {
                Offset = offset;
                GuideX = guideX;
                GuideY = guideY;
            }
        }

        public static Result Snap(Rect moving, List<Rect> others, float threshold)
        {
            var (dx, guideX) = Best(new[] { moving.xMin, moving.center.x, moving.xMax }, others, threshold, true);
            var (dy, guideY) = Best(new[] { moving.yMin, moving.center.y, moving.yMax }, others, threshold, false);
            return new Result(new Vector2(dx, dy), guideX, guideY);
        }

        private static (float delta, float? guide) Best(float[] movingLines, List<Rect> others, float threshold, bool horizontal)
        {
            // 全候補の中で最も近い一致を1つ選ぶ。閾値内に無ければ吸着しない
            // Pick the single closest match among all candidates; no snap if none is within the threshold
            var bestDistance = threshold;
            var bestDelta = 0f;
            float? guide = null;
            foreach (var other in others)
            {
                for (var k = 0; k < 3; k++)
                {
                    var line = Line(other, k, horizontal);
                    foreach (var movingLine in movingLines)
                    {
                        var distance = Mathf.Abs(line - movingLine);
                        if (distance >= bestDistance) continue;
                        bestDistance = distance;
                        bestDelta = line - movingLine;
                        guide = line;
                    }
                }
            }
            return (bestDelta, guide);
        }

        private static float Line(Rect rect, int index, bool horizontal)
        {
            if (horizontal) return index == 0 ? rect.xMin : index == 1 ? rect.center.x : rect.xMax;
            return index == 0 ? rect.yMin : index == 1 ? rect.center.y : rect.yMax;
        }
    }
}
