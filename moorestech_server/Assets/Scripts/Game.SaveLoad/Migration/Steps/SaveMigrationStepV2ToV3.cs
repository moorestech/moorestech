using System.Linq;
using Game.SaveLoad.Migration.Steps.V2ToV3;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V2→V3移行（ADR0073）
    // V2→V3 migration (ADR 0073)
    // - 旧ランダムID→連番 / players節は全員持ち主未定で作成
    // - legacy random id→sequential / players section built all-unclaimed
    public sealed class SaveMigrationStepV2ToV3 : ISaveMigrationStep
    {
        public int FromVersion => 2;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            // 版2に players 節は無い。あるなら想定外の形なので変換しない
            // V2 has no players section; its presence is an unexpected shape, so refuse
            if (save["players"] != null) return Fail("版2のセーブに players 節が既にある");

            if (!PlayerIdRenumbering.TryBuildMap(save, out var map, out var reason)) return Fail(reason);
            if (!PlayerClaimCandidateDataValidator.TryValidate(save, out reason)) return Fail(reason);
            PlayerIdRenumbering.Apply(save, map);

            // 候補は振り直し後の節から選ぶ
            // Choose the candidate from the renumbered sections
            var candidate = PlayerClaimCandidateSelector.Select(save, map);
            var entries = new JArray(map.Values.OrderBy(id => id).Select(id => new JObject { ["playerId"] = id, ["identity"] = null }));
            save["players"] = new JObject
            {
                ["nextPlayerId"] = map.Count + 1,
                ["claimCandidatePlayerId"] = candidate.HasValue ? new JValue(candidate.Value) : JValue.CreateNull(),
                ["entries"] = entries,
            };

            Debug.Log($"セーブを版2から版3へ変換しました。プレイヤーID振り直し={map.Count}件 結びつけ候補={(candidate.HasValue ? candidate.Value.ToString() : "なし")}");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            SaveMigrationStepResult Fail(string reason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版2から版3へ変換できません: {reason}");
                return SaveMigrationStepResult.Failed(reason);
            }

            #endregion
        }
    }
}
