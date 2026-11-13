using Game.SaveLoad.Migration.Steps.Shared;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V10ToV11
{
    // V10→V11: 床・壁パネルが5マス幅になったため、旧1マスのパネルを world とブループリントから撤去する
    // V10->V11: floor/wall panels became five cells wide, so remove old one-cell panels from the world and blueprints
    public sealed class SaveMigrationStepV10ToV11 : ISaveMigrationStep
    {
        public int FromVersion => 10;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            var parts = new ISaveMigrationPart[]
            {
                new CleanRoomPanelWorldRemover(),
                new CleanRoomPanelBlueprintRemover(),
            };
            return MigrationSaveJson.RunParts(FromVersion, parts, save);
        }
    }
}
