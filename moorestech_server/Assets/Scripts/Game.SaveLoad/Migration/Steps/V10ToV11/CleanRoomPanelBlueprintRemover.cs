using System.Collections.Generic;
using Game.SaveLoad.Migration.Steps.Shared;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.V10ToV11
{
    /// <summary>
    /// ブループリント内の旧1マスの床・壁パネルを外し、電線・チェーンの添字を詰める
    /// Removes old one-cell floor/wall panels from blueprints and compacts wire/chain indices
    /// </summary>
    internal sealed class CleanRoomPanelBlueprintRemover : ISaveMigrationPart
    {
        private const string BlueprintsKey = "blueprints";
        private const string BlockGuidKey = "blockGuid";

        private readonly List<(JObject blueprint, List<int> panelIndices)> _targets = new();

        public bool TryPrepare(JObject save, out string failureReason)
        {
            failureReason = null;
            var blueprintsToken = save[BlueprintsKey];
            if (MigrationSaveJson.IsAbsentOrNull(blueprintsToken)) return true;
            if (!(blueprintsToken is JArray blueprints)) return MigrationSaveJson.Reject($"セーブのblueprintsが配列ではありません。 type={blueprintsToken.Type}", out failureReason);

            foreach (var blueprintToken in blueprints)
            {
                if (!(blueprintToken is JObject blueprint)) return MigrationSaveJson.Reject($"blueprints要素がオブジェクトではありません。 type={blueprintToken.Type}", out failureReason);
                if (!TryReadBlueprint(blueprint, out var panelIndices, out failureReason)) return false;
                if (panelIndices.Count > 0) _targets.Add((blueprint, panelIndices));
            }

            return true;

            #region Internal

            // 形と添字範囲を検証しパネルの添字を集める
            // Validate shapes and index ranges, collect panel indices
            bool TryReadBlueprint(JObject blueprint, out List<int> panelIndices, out string failureReason)
            {
                panelIndices = new List<int>();
                var name = blueprint["name"];
                if (!(blueprint["blocks"] is JArray blocks)) return MigrationSaveJson.Reject($"ブループリントのblocksが配列ではありません。 blueprint={name} type={blueprint["blocks"]?.Type}", out failureReason);

                for (var i = 0; i < blocks.Count; i++)
                {
                    var guidToken = (blocks[i] as JObject)?[BlockGuidKey];
                    if (guidToken?.Type != JTokenType.String) return MigrationSaveJson.Reject($"ブループリント内ブロックのblockGuidが読めません。 blueprint={name} index={i}", out failureReason);
                    if (Version10CleanRoomPanelGuids.Contains(guidToken.Value<string>())) panelIndices.Add(i);
                }

                foreach (var lineKey in BlueprintBlockRemoval.LineKeys)
                {
                    if (!(blueprint[lineKey] is JArray lines)) return MigrationSaveJson.Reject($"ブループリントの{lineKey}が配列ではありません。 blueprint={name} type={blueprint[lineKey]?.Type}", out failureReason);
                    foreach (var line in lines)
                    {
                        if (line is JObject lineObject && IsIndexInRange(lineObject[BlueprintBlockRemoval.BlockIndexAKey], blocks.Count) && IsIndexInRange(lineObject[BlueprintBlockRemoval.BlockIndexBKey], blocks.Count)) continue;
                        return MigrationSaveJson.Reject($"ブループリントの{lineKey}の添字が読めないか範囲外です。 blueprint={name} line={line.ToString(Newtonsoft.Json.Formatting.None)} blocks={blocks.Count}", out failureReason);
                    }
                }

                failureReason = null;
                return true;
            }

            bool IsIndexInRange(JToken token, int count)
            {
                if (token == null || token.Type != JTokenType.Integer) return false;
                var index = token.Value<long>();
                return 0 <= index && index < count;
            }

            #endregion
        }

        public string ApplyAndSummarize()
        {
            if (_targets.Count == 0) return "ブループリントの床・壁パネル 対象なし";

            var removedCount = 0;
            foreach (var (blueprint, panelIndices) in _targets)
            {
                BlueprintBlockRemoval.RemoveAt(blueprint, panelIndices);
                removedCount += panelIndices.Count;
                Debug.LogWarning($"ブループリント内の旧1マスの床・壁パネルを外しました。 blueprint={blueprint["name"]} 件数={panelIndices.Count}");
            }

            return $"ブループリントの床・壁パネルを撤去={removedCount}件";
        }
    }
}
