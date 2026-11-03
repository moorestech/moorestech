using Game.SaveLoad.Migration.Steps.Shared;
using Game.SaveLoad.Migration.Steps.V9ToV10;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps
{
    // V9→V10移行: 縦向き(Up*/Down*)の廃止に合わせ、向きを水平4方向の番号へ読み替え、縦置きを寝かせる
    // V9->V10 migration: with Up*/Down* removed, renumber directions to the four horizontal headings and lay vertical blocks flat
    public sealed class SaveMigrationStepV9ToV10 : ISaveMigrationStep
    {
        public int FromVersion => 9;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            var parts = new ISaveMigrationPart[]
            {
                new VerticalDirectionWorldRewriter(),
                new VerticalDirectionBlueprintRewriter(),
            };
            return MigrationSaveJson.RunParts(FromVersion, parts, save);
        }
    }
}
