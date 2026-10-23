using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V6→V7移行: 宇宙エレベーターのケーブルプールの節を0で足す。版6まではケーブルを軌道へ運ぶ積荷表の行が無く、0が実値である
    // V6→V7 migration: add the space elevator cable pool section at zero; up to version 6 no payload row could carry cables to orbit, so zero is the real value
    public sealed class SaveMigrationStepV6ToV7 : ISaveMigrationStep
    {
        private const string PoolKey = "spaceElevatorCablePool";
        private const string CountKey = "cableCount";

        public int FromVersion => 6;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            // 既にあれば形だけ確かめて素通しする（冪等）。値の範囲（負でないか）はロード時の Restore だけが見る
            // When present, only check the shape and pass it through (idempotent); only Restore at load checks the value range
            var existing = save[PoolKey];
            if (existing == null)
            {
                save[PoolKey] = new JObject { [CountKey] = 0 };
                Debug.Log("セーブを版6から版7へ変換しました。宇宙エレベーターのケーブルプールを0で足しました");
                return SaveMigrationStepResult.Converted(save);
            }
            if (existing is JObject pool && pool[CountKey]?.Type == JTokenType.Integer) return SaveMigrationStepResult.Converted(save);
            return Fail($"{PoolKey} が整数の {CountKey} を持つオブジェクトではありません。 value={existing}");

            #region Internal

            SaveMigrationStepResult Fail(string failureReason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版6から版7へ変換できません: {failureReason}");
                return SaveMigrationStepResult.Failed(failureReason);
            }

            #endregion
        }
    }
}
