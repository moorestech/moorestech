using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // - 初発射履歴を未発射で追加
    // - ADR 0088に従う
    // - Add first-launch history as not yet launched
    // - Follow ADR 0088
    public sealed class SaveMigrationStepV6ToV7 : ISaveMigrationStep
    {
        public int FromVersion => 6;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            // 版6に履歴は無い。あるなら想定外の形なので変換しない
            // V6 has no launch history; its presence is an unexpected shape, so refuse
            if (save["rocketHasLaunchedOnce"] is JToken existing && existing.Type != JTokenType.Null) return Fail("版6のセーブに rocketHasLaunchedOnce が既にある");

            // 版6以前に発射台は存在しないので、未打ち上げが唯一の正しい値
            // No launch pad existed in V6 or earlier, so not-yet-launched is the only correct value
            save["rocketHasLaunchedOnce"] = false;
            Debug.Log("セーブを版6から版7へ変換しました。ロケットの初打ち上げ履歴=未打ち上げ");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            SaveMigrationStepResult Fail(string reason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版6から版7へ変換できません: {reason}");
                return SaveMigrationStepResult.Failed(reason);
            }

            #endregion
        }
    }
}
