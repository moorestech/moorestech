using System.Collections.Generic;
using Mooresmaster.Model.GenerationModule;

namespace Core.Master
{
    // 複数modのgeneration.json候補からpriority最大の1件を選ぶ純粋関数。
    // algorithm==Noneは除外し、同priorityはmodId文字列のOrdinal昇順で若い方を採用する。
    // Pure selection over multi-mod generation.json candidates: picks max priority.
    // Entries with algorithm==None are excluded; ties break by modId Ordinal ascending.
    public static class GenerationSelection
    {
        public static Generation Select(IReadOnlyList<(Generation Element, string ModId)> candidates)
        {
            Generation selected = null;
            string selectedModId = null;

            foreach (var candidate in candidates)
            {
                // 生成器を提供しないmodは選択対象から除外
                // Skip mods that don't provide a generator
                if (candidate.Element.Algorithm == Generation.AlgorithmConst.None)
                {
                    continue;
                }

                if (!IsBetter(candidate)) continue;
                selected = candidate.Element;
                selectedModId = candidate.ModId;
            }

            return selected;

            #region Internal

            bool IsBetter((Generation Element, string ModId) candidate)
            {
                if (selected == null) return true;
                if (candidate.Element.Priority != selected.Priority) return candidate.Element.Priority > selected.Priority;
                return string.CompareOrdinal(candidate.ModId, selectedModId) < 0;
            }

            #endregion
        }
    }
}
