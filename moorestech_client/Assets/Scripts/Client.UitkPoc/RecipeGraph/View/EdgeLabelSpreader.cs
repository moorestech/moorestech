using UnityEngine;

namespace Client.UitkPoc.RecipeGraph.View
{
    // 重なった辺ラベルを縦方向へ押し分ける簡易な衝突解消
    // Simple collision pass pushing overlapping edge labels apart vertically
    public static class EdgeLabelSpreader
    {
        public static void Spread(Vector2[] anchors, float labelWidth, float labelHeight)
        {
            for (var iteration = 0; iteration < 8; iteration++)
            {
                var moved = false;
                for (var a = 0; a < anchors.Length; a++)
                {
                    for (var b = a + 1; b < anchors.Length; b++) moved |= Separate(a, b);
                }
                if (!moved) return;
            }

            #region Internal

            bool Separate(int a, int b)
            {
                var delta = anchors[b] - anchors[a];
                if (Mathf.Abs(delta.x) >= labelWidth || Mathf.Abs(delta.y) >= labelHeight) return false;

                // 重なり量の半分ずつ上下へ逃がす
                // Push each half of the overlap up and down
                var push = (labelHeight - Mathf.Abs(delta.y)) / 2f + 0.5f;
                var direction = delta.y >= 0 ? 1f : -1f;
                anchors[a].y -= push * direction;
                anchors[b].y += push * direction;
                return true;
            }

            #endregion
        }
    }
}
