using System.Collections.Generic;
using Game.SaveLoad.Migration.Steps.V8ToV9;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V8→V9移行: ウェハグレード廃止に合わせ、グレード付きウェハをG1へ寄せ、版9で読めない旧抽選記録と継承をやめたレシピの継承レベルを捨てる
    // V8->V9 migration: with wafer grades removed, fold graded wafers into G1 and drop legacy draw records and inherited levels of recipes that stopped inheriting
    public sealed class SaveMigrationStepV8ToV9 : ISaveMigrationStep
    {
        public int FromVersion => 8;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            var parts = new IV8ToV9MigrationPart[]
            {
                new WaferGradeItemGuidRewriter(),
                new CleanRoomLegacyDrawStateCleaner(),
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

            Debug.Log($"セーブを版8から版9へ変換しました。{string.Join(" / ", summaries)}");
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
