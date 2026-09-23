using Newtonsoft.Json.Linq;
namespace Game.SaveLoad.Migration.Steps
{
    public sealed class SaveMigrationStepV2ToV3 : ISaveMigrationStep
    {
        public int FromVersion => 2;
        public SaveMigrationStepResult Migrate(JObject save)
            => SaveMigrationStepResult.Failed("Version 3 is a new-world belt segment prototype. Create a new world; migration of version 1/2 worlds is deferred. The original save is preserved.");
    }
}
