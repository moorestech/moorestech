using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Server.MasterGate.Editor
{
    /// <summary>
    ///     G2（tools/progression/check.py --emit-order）が書いた研究順の1行。研究GUIDと、その研究で付くはずの解放
    ///     One row of the research order G2 (tools/progression/check.py --emit-order) wrote: the research GUID and the unlocks it should apply
    /// </summary>
    public readonly struct ResearchWalkStep
    {
        public readonly Guid ResearchNodeGuid;
        public readonly string Name;
        public readonly IReadOnlyList<Guid> UnlockBlockGuids;
        public readonly IReadOnlyList<Guid> UnlockMachineRecipeGuids;

        public ResearchWalkStep(Guid researchNodeGuid, string name, IReadOnlyList<Guid> unlockBlockGuids, IReadOnlyList<Guid> unlockMachineRecipeGuids)
        {
            ResearchNodeGuid = researchNodeGuid;
            Name = name;
            UnlockBlockGuids = unlockBlockGuids;
            UnlockMachineRecipeGuids = unlockMachineRecipeGuids;
        }
    }

    public static class ResearchWalkOrderFile
    {
        public static bool TryRead(string path, out List<ResearchWalkStep> steps, out string failure)
        {
            steps = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                failure = $"[ResearchWalk] FAIL research order file not found: '{path}' (G2 を --emit-order 付きで先に回す)";
                Debug.LogError(failure);
                return false;
            }

            // 研究順は別プロセス（Python）が書いた外部入力JSON（境界）。壊れていれば理由を残して失敗にする
            // The order is external-input JSON written by another process (Python), a boundary; a broken file fails with a reason
            try
            {
                steps = JObject.Parse(File.ReadAllText(path))["order"].Select(ToStep).ToList();
            }
            catch (Exception exception)
            {
                failure = $"[ResearchWalk] FAIL research order file is unreadable: {path}\n{exception}";
                Debug.LogError(failure);
                return false;
            }

            failure = null;
            return true;

            #region Internal

            static ResearchWalkStep ToStep(JToken row)
            {
                return new ResearchWalkStep(Guid.Parse((string)row["researchNodeGuid"]), (string)row["name"],
                    row["unlockBlockGuids"].Select(g => Guid.Parse((string)g)).ToList(),
                    row["unlockMachineRecipeGuids"].Select(g => Guid.Parse((string)g)).ToList());
            }

            #endregion
        }
    }
}
