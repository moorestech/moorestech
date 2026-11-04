using Game.SaveLoad.Migration.Steps.Shared;
using Game.SaveLoad.Migration.Steps.V9ToV10;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps
{
    // V9→V10: 縦向き廃止に伴う向き読替と縦置きの寝かせ
    // V9->V10: renumber directions and lay vertical blocks flat after vertical removal
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
