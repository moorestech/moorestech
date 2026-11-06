using Game.SaveLoad.Migration.Steps.Shared;
using Game.SaveLoad.Migration.Steps.V9ToV10;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps
{
    // V9→V10: 縦向き廃止に伴う向き読替と縦置きの寝かせ
    // V9->V10: renumber directions and lay vertical blocks flat after vertical removal
    // ブロックサイズのためロード時点のマスタを引く（マスタ欠損のブロックは占有に数えない）
    // Looks up the master loaded at this point for block sizes (blocks missing from the master are not counted as occupying)
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
