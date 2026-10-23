using Game.Orbit;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Boot.Composition
{
    // 軌道到着と初打ち上げ履歴をワールドに1つずつ置く
    // Registers one orbital arrival service and one launch history per world
    internal static class OrbitServiceRegistration
    {
        internal static void Register(ServiceCollection services)
        {
            services.AddSingleton<RocketLaunchHistory>();
            services.AddSingleton<IOrbitalArrivalService, OrbitalArrivalService>();
        }
    }
}
