using System;
using System.Linq;
using Core.Master;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning.Sections
{
    /// <summary>research節: マスタに無い研究ノードを完了一覧から除去する</summary>
    /// <summary>The research section: removes research nodes absent from the master out of the completed list</summary>
    public sealed class ResearchSectionPruner : IMissingMasterSectionPruner
    {
        // ResearchSaveJsonObjectはJsonProperty無しなのでプロパティ名がそのまま綴られる
        // ResearchSaveJsonObject carries no JsonProperty, so the property name itself is the spelling
        private const string CompletedResearchGuidsKey = "CompletedResearchGuids";

        public string SaveSectionName => "research";

        public MissingMasterSectionPruneResult Prune(JToken section)
        {
            var result = new MissingMasterSectionPruneResult();
            if (section is not JObject research || research[CompletedResearchGuidsKey] is not JArray completed)
            {
                Debug.Log($"research.{CompletedResearchGuidsKey}が配列でないため研究ノードの除去を行いません。");
                return result;
            }

            foreach (var entry in completed.ToList())
            {
                // 読めないguidは完了扱いのまま残る。研究解放の判定に響くので警告する
                // An unreadable guid stays marked complete, which skews unlock checks, so warn
                var guidText = entry.Type == JTokenType.String ? entry.Value<string>() : entry.ToString();
                if (!Guid.TryParse(guidText, out var guid))
                {
                    Debug.LogWarning($"研究guidがguidとして読めないため除去判定せず残します。 researchGuid={guidText}");
                    continue;
                }

                if (MasterHolder.ResearchMaster.GetResearch(guid) != null) continue;

                Debug.LogWarning($"マスタに存在しない研究ノードを完了一覧から除去します。 researchGuid={guidText}");
                result.RemovedResearchGuids.Add(guidText);
                entry.Remove();
            }

            return result;
        }
    }
}
