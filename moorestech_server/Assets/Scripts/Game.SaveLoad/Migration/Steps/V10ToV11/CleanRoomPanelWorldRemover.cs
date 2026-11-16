using System.Collections.Generic;
using System.Linq;
using Game.SaveLoad.Migration.Steps.Shared;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.V10ToV11
{
    /// <summary>
    /// world に置かれた旧1マスの床・壁パネルを撤去する（アイテムは返さない）
    /// Removes the old one-cell floor/wall panels placed in the world (items are not refunded)
    /// </summary>
    internal sealed class CleanRoomPanelWorldRemover : ISaveMigrationPart
    {
        private const string WorldKey = "world";
        private const string BlockGuidKey = "blockGuid";

        private readonly List<JObject> _panels = new();

        public bool TryPrepare(JObject save, out string failureReason)
        {
            if (!(save[WorldKey] is JArray world)) return MigrationSaveJson.Reject($"セーブのworldが配列ではありません。 type={save[WorldKey]?.Type}", out failureReason);

            // 形を検査しながらパネルだけ集める
            // Collect only panels while checking shapes
            foreach (var blockToken in world)
            {
                if (!(blockToken is JObject block)) return MigrationSaveJson.Reject($"world要素がオブジェクトではありません。 type={blockToken.Type}", out failureReason);
                if (block[BlockGuidKey]?.Type != JTokenType.String) return MigrationSaveJson.Reject($"world要素のblockGuidが文字列ではありません。 instanceId={block["instanceId"]} type={block[BlockGuidKey]?.Type}", out failureReason);
                if (Version10CleanRoomPanelGuids.Contains(block[BlockGuidKey].Value<string>())) _panels.Add(block);
            }

            failureReason = null;
            return true;
        }

        public string ApplyAndSummarize()
        {
            if (_panels.Count == 0) return "床・壁パネルの撤去 対象なし";

            // 撤去した位置を開発者が追えるようログに残す
            // Log removed positions so developers can trace them
            var targets = string.Join(", ", _panels.Select(block => $"{block[BlockGuidKey]}@({block["X"]},{block["Y"]},{block["Z"]})"));
            foreach (var panel in _panels) panel.Remove();
            Debug.LogWarning($"床・壁パネルが5マス幅になったため、旧1マスのパネルを撤去しました。 件数={_panels.Count} 対象=[{targets}]");
            return $"床・壁パネルを撤去={_panels.Count}件";
        }
    }
}
