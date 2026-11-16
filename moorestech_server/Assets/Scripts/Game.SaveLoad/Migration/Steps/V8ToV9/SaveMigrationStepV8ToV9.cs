using Newtonsoft.Json.Linq;
using Game.SaveLoad.Migration.Steps.Shared;

namespace Game.SaveLoad.Migration.Steps.V8ToV9
{
    // V8→V9移行: ウェハグレード廃止に合わせ、グレード付きウェハをG1へ寄せ、版9で読めない旧抽選記録と継承をやめたレシピの継承レベルを捨てる
    // V8->V9 migration: with wafer grades removed, fold graded wafers into G1 and drop legacy draw records and inherited levels of recipes that stopped inheriting
    public sealed class SaveMigrationStepV8ToV9 : ISaveMigrationStep
    {
        public int FromVersion => 8;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            var parts = new ISaveMigrationPart[]
            {
                new WaferGradeItemGuidRewriter(),
                new CleanRoomLegacyDrawStateCleaner(),
            };
            return MigrationSaveJson.RunParts(FromVersion, parts, save);
        }
    }
}
