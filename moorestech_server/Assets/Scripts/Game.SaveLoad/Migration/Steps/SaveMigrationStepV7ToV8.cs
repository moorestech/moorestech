using System.Collections.Generic;
using Game.SaveLoad.Migration.Steps.V7ToV8;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V7→V8移行: Tier9統合で変わるセーブの形を機能ごとの部品で直す（継承レベル・清浄室機械の液体スロット・初回打ち上げ履歴・ケーブルプール・コスト0研究のリセット）
    // V7->V8 migration: adjust the save shapes changed by the Tier9 integration through one part per feature (inherited level, clean-room fluid slots, first-launch history, cable pool, zero-cost research reset)
    public sealed class SaveMigrationStepV7ToV8 : ISaveMigrationStep
    {
        public int FromVersion => 7;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            var parts = new IV7ToV8MigrationPart[]
            {
                new MachineInheritedLevelFiller(),
                new CleanRoomMachineFluidSlotFiller(),
                new RocketLaunchHistoryFiller(),
                new SpaceElevatorCablePoolFiller(),
                new ZeroCostTier9ResearchResetter(),
            };

            // 先に全部品を検査し、どれかが拒否したら原本を書き換えない
            // Inspect every part first so a refusal never leaves the original half-rewritten
            foreach (var part in parts)
            {
                if (!part.TryPrepare(save, out var reason)) return Fail(reason);
            }

            // 検査を通った対象だけを書き換える
            // Rewrite only the targets that passed inspection
            var summaries = new List<string>();
            foreach (var part in parts) summaries.Add(part.ApplyAndSummarize());

            Debug.Log($"セーブを版7から版8へ変換しました。{string.Join(" / ", summaries)}");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            SaveMigrationStepResult Fail(string failureReason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版7から版8へ変換できません: {failureReason}");
                return SaveMigrationStepResult.Failed(failureReason);
            }

            #endregion
        }
    }
}
