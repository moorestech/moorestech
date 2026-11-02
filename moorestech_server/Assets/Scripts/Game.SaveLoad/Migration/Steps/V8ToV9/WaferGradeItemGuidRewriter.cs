using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Game.SaveLoad.Migration.Steps.Shared;

namespace Game.SaveLoad.Migration.Steps.V8ToV9
{
    /// <summary>
    /// セーブ中の G2〜G5 ウェハの GUID 文字列を同じ工程の G1 へ置き換える（冪等）。スタック・消費実品の配列・ベルト上の品など、置き場を問わず値で置き換える
    /// Replace G2-G5 wafer GUID strings in the save with their process's G1 (idempotent); replaced by value wherever they sit, stacks, consumed-input arrays, belt items alike
    /// </summary>
    public sealed class WaferGradeItemGuidRewriter : ISaveMigrationPart
    {
        // 解放状態は guid ごとに後勝ちで読まれる。G1 へ寄せると未解放の G5 が解放済みの G1 を上書きするので触らない（消えた品の行は後段の MissingMasterPruner が除く）
        // Unlock states are read last-wins per guid; folding into G1 would let a locked G5 overwrite an unlocked G1, so leave them (MissingMasterPruner drops the removed items' rows later)
        private const string GameUnlockStateKey = "gameUnlockState";

        private readonly List<(JValue token, Guid baseGuid)> _targets = new();

        public bool TryPrepare(JObject save, out string failureReason)
        {
            failureReason = null;

            // 解放状態の節を除いた全値を辿り、消えるウェハの GUID 文字列を集める
            // Walk every value outside the unlock-state section and collect removed-wafer GUID strings
            foreach (var property in save.Properties())
            {
                if (property.Name == GameUnlockStateKey) continue;
                foreach (var value in property.Descendants().OfType<JValue>())
                {
                    if (value.Type != JTokenType.String) continue;
                    if (!Guid.TryParse((string)value.Value, out var guid)) continue;
                    if (WaferGradeRemovalTable.RemovedToBaseWaferGuids.TryGetValue(guid, out var baseGuid)) _targets.Add((value, baseGuid));
                }
            }

            return true;
        }

        public string ApplyAndSummarize()
        {
            foreach (var (token, baseGuid) in _targets) token.Value = baseGuid.ToString();
            return $"G1へ寄せたグレード付きウェハのGUID={_targets.Count}箇所";
        }
    }
}
