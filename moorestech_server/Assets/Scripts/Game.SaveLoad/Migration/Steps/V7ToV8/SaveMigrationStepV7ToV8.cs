using Newtonsoft.Json.Linq;
using Game.SaveLoad.Migration.Steps.Shared;

namespace Game.SaveLoad.Migration.Steps.V7ToV8
{
    // V7→V8移行: Tier9統合で変わるセーブの形を機能ごとの部品で直す（継承レベル・清浄室機械の液体スロット・初回打ち上げ履歴・ケーブルプール・コスト0研究のリセット）
    // V7->V8 migration: adjust the save shapes changed by the Tier9 integration through one part per feature (inherited level, clean-room fluid slots, first-launch history, cable pool, zero-cost research reset)
    public sealed class SaveMigrationStepV7ToV8 : ISaveMigrationStep
    {
        public int FromVersion => 7;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            var parts = new ISaveMigrationPart[]
            {
                new MachineInheritedLevelFiller(),
                new ElectricGeneratorFuelStateNormalizer(),
                new CleanRoomMachineFluidSlotFiller(),
                new RocketLaunchHistoryFiller(),
                new SpaceElevatorCablePoolFiller(),
                new ZeroCostTier9ResearchResetter(),
            };
            return MigrationSaveJson.RunParts(FromVersion, parts, save);
        }
    }
}
