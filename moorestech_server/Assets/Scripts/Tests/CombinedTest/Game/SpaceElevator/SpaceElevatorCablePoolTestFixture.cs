using Microsoft.Extensions.DependencyInjection;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    internal static class SpaceElevatorCablePoolTestFixture
    {
        internal static ServiceProvider CreateServer()
        {
            // 本物の拠点テンプレートでプール境界も検証する
            // Exercise pool boundaries with the production base template
            var (_, provider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return provider;
        }
    }
}
