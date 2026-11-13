using Game.SaveLoad.Migration.Steps.Shared;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V11ToV12
{
    // V11→V12: 不純物量削除・摩耗を経過秒へ置換
    // V11->V12: drop room impurity and replace purifier wear with elapsed seconds
    public sealed class SaveMigrationStepV11ToV12 : ISaveMigrationStep
    {
        public int FromVersion => 11;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            var parts = new ISaveMigrationPart[]
            {
                new CleanRoomImpurityRemover(),
                new CleanRoomAirFilterWearConverter(),
            };
            return MigrationSaveJson.RunParts(FromVersion, parts, save);
        }
    }
}
