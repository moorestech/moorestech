using System.Collections.Generic;
using Game.SaveLoad.Migration.Steps.V8ToV9;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V8→V9移行: 清浄室機械のstateへ空の液体スロット配列を足し、未実装の仮ブロック3種の保存済み解放を戻す
    // V8->V9 migration: add empty fluid slot arrays to clean-room machine states and revert saved unlocks of three unimplemented placeholder blocks
    public sealed class SaveMigrationStepV8ToV9 : ISaveMigrationStep
    {
        public int FromVersion => 8;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            if (!(save["world"] is JArray world)) return Fail($"セーブのworldが配列ではないため変換できません。 type={save["world"]?.Type}");

            // 先に両方の対象を検査し、途中で拒否しても原本を書き換えない
            // Inspect both target sets first so a refusal never leaves the original half-rewritten
            var fluidTargets = new List<JObject>();
            if (!CleanRoomMachineFluidSlotFiller.TryCollectTargets(world, fluidTargets, out var fluidFailure)) return Fail(fluidFailure);
            var unlockTargets = new List<JObject>();
            if (!UnimplementedBlockUnlockRevoker.TryCollectTargets(save, unlockTargets, out var unlockFailure)) return Fail(unlockFailure);

            // 検査を通った対象だけを書き換える
            // Rewrite only the targets that passed inspection
            var filledCount = CleanRoomMachineFluidSlotFiller.Fill(fluidTargets);
            var revokedCount = UnimplementedBlockUnlockRevoker.Revoke(unlockTargets);

            Debug.Log($"セーブを版8から版9へ変換しました。液体スロットを足した清浄室機械={filledCount}台 解放を戻した仮ブロック={revokedCount}種");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            SaveMigrationStepResult Fail(string failureReason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版8から版9へ変換できません: {failureReason}");
                return SaveMigrationStepResult.Failed(failureReason);
            }

            #endregion
        }
    }
}
