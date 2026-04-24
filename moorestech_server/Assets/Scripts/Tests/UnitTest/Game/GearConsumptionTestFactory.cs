using Mooresmaster.Model.GearConsumptionModule;

namespace Tests.UnitTest.Game
{
    // テスト専用のGearConsumption生成ヘルパー。プロダクションコードはマスタJSON経由で生成する
    // Test-only helper for constructing GearConsumption. Production code builds it from master JSON.
    internal static class GearConsumptionTestFactory
    {
        public static GearConsumption Create(
            double baseRpm,
            double minimumRpm,
            double baseTorque,
            double torqueExponentUnder,
            double torqueExponentOver)
        {
            return new GearConsumption(
                (float)baseRpm,
                (float)minimumRpm,
                (float)baseTorque,
                (float)torqueExponentUnder,
                (float)torqueExponentOver);
        }
    }
}
