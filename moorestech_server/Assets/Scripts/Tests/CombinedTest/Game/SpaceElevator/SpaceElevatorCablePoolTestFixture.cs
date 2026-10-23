using Microsoft.Extensions.DependencyInjection;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    internal static class SpaceElevatorCablePoolTestFixture
    {
        internal static ServiceProvider CreateServer()
        {
            // 実拠点テンプレートでプール境界を検証
            // Verify pool boundaries using the production base template
            var (_, provider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return provider;
        }
    }
}
