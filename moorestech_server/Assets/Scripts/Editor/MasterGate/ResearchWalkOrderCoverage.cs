using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.MasterGate.Editor
{
    /// <summary>
    ///     G3 が「全ノード」を歩くことの保証。研究順の GUID 集合が、ロード済みマスタの全研究 GUID と重複なく一致するかを歩く前に確かめる
    ///     Guarantees G3 walks every node: before walking, check the order's GUIDs match all loaded research GUIDs exactly, with no duplicates
    /// </summary>
    internal static class ResearchWalkOrderCoverage
    {
        internal static bool TryCheck(IReadOnlyList<ResearchWalkStep> steps, IReadOnlyCollection<Guid> loadedResearchGuids, out string failure)
        {
            // G2 は未到達の研究を順に書かないので、部分的な順や空の順を完走しても全ノードの完了にはならない
            // G2 omits unreachable research, so finishing a partial or empty order does not mean every node completed
            if (steps.Count == 0)
            {
                failure = $"[ResearchWalk] FAIL research order is empty (loaded research={loadedResearchGuids.Count})";
                return false;
            }

            // 重複・未知（マスタに無い）・欠落（順に無い）を、それぞれ全件まとめて理由に出す
            // Report every duplicate, unknown (absent from the master) and missing (absent from the order) GUID in the reason
            var loaded = new HashSet<Guid>(loadedResearchGuids);
            var ordered = new HashSet<Guid>(steps.Select(step => step.ResearchNodeGuid));
            var duplicates = steps.GroupBy(step => step.ResearchNodeGuid).Where(group => 1 < group.Count()).Select(group => $"{group.First().Name} ({group.Key}) x{group.Count()}").ToList();
            var unknown = steps.Where(step => !loaded.Contains(step.ResearchNodeGuid)).Select(step => $"{step.Name} ({step.ResearchNodeGuid})").Distinct().ToList();
            var missing = loaded.Where(guid => !ordered.Contains(guid)).Select(guid => guid.ToString()).ToList();
            if (duplicates.Count == 0 && unknown.Count == 0 && missing.Count == 0)
            {
                failure = null;
                return true;
            }

            failure = $"[ResearchWalk] FAIL research order does not cover the loaded research exactly (order={steps.Count}, loaded research={loaded.Count})" +
                      Section("duplicated in order", duplicates) + Section("unknown to the master", unknown) + Section("missing from order", missing);
            return false;

            #region Internal

            static string Section(string label, List<string> entries)
            {
                return entries.Count == 0 ? "" : $"\n  {label} ({entries.Count}): {string.Join(", ", entries)}";
            }

            #endregion
        }
    }
}
