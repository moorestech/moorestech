using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.Shared
{
    /// <summary>
    /// ブループリントからブロックを添字で外し、端が消えた電線・チェーンを除いて残りの添字を詰める
    /// Removes blueprint blocks by index, drops wires/chains that lost an end, and compacts the remaining indices
    /// </summary>
    public static class BlueprintBlockRemoval
    {
        public static readonly string[] LineKeys = { "wires", "chains" };
        public const string BlockIndexAKey = "blockIndexA";
        public const string BlockIndexBKey = "blockIndexB";

        // 呼び出し側で blocks と各線の添字が範囲内の整数であることを検査済みとする
        // The caller has already verified that blocks and every line index are in-range integers
        public static void RemoveAt(JObject blueprint, IReadOnlyCollection<int> removedIndices)
        {
            var blocks = (JArray)blueprint["blocks"];
            var removedSet = new HashSet<int>(removedIndices);

            // 残るブロックの新しい添字を振る
            // Assign new indices to the surviving blocks
            var newIndexByOldIndex = new Dictionary<int, int>();
            for (var oldIndex = 0; oldIndex < blocks.Count; oldIndex++)
            {
                if (removedSet.Contains(oldIndex)) continue;
                newIndexByOldIndex[oldIndex] = newIndexByOldIndex.Count;
            }

            var removedTokens = removedSet.Select(index => blocks[index]).ToList();
            foreach (var token in removedTokens) token.Remove();

            // 端が消えた線は外し、残る線は添字を付け替える
            // Drop lines that lost an end and remap the rest
            foreach (var lineKey in LineKeys)
            {
                if (!(blueprint[lineKey] is JArray lines)) continue;
                foreach (var line in lines.OfType<JObject>().ToList())
                {
                    var hasA = newIndexByOldIndex.TryGetValue(line[BlockIndexAKey].Value<int>(), out var newA);
                    var hasB = newIndexByOldIndex.TryGetValue(line[BlockIndexBKey].Value<int>(), out var newB);
                    if (!hasA || !hasB)
                    {
                        line.Remove();
                        continue;
                    }

                    line[BlockIndexAKey] = newA;
                    line[BlockIndexBKey] = newB;
                }
            }
        }
    }
}
