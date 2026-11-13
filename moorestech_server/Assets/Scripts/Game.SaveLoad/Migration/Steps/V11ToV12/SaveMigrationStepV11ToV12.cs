using Game.SaveLoad.Migration.Steps.Shared;
using Game.SaveLoad.Migration.Steps.V11ToV12;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps
{
    // V11→V12: クリーンルームの濃度を廃止しフィルターを定率消費にしたため、部屋の不純物量を消し、清浄機の摩耗を経過秒へ置き換える
    // V11->V12: clean-room concentration was removed and filters now wear at a fixed rate, so drop room impurity and replace purifier wear with elapsed seconds
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
