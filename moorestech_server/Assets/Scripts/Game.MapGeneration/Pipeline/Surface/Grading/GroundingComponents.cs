using System.Collections.Generic;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Grading
{
    internal static class GroundingComponents
    {
        internal static int[] Build(IReadOnlyList<RectInt> supports)
        {
            var roots = new int[supports.Count];
            for (int i = 0; i < roots.Length; i++) roots[i] = i;

            // 支持頂点を共有するcoreだけを推移的に結合する
            // Transitively join only cores sharing interpolation support vertices
            for (int i = 0; i < roots.Length; i++)
            for (int j = i + 1; j < roots.Length; j++)
                if (supports[i].Overlaps(supports[j])) roots[Root(j)] = Root(i);
            for (int i = 0; i < roots.Length; i++) roots[i] = Root(i);
            return roots;

            #region Internal

            int Root(int index)
            {
                while (roots[index] != index) index = roots[index];
                return index;
            }

            #endregion
        }
    }
}
