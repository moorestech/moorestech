using Game.SpaceElevator;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Boot.Composition
{
    // プールと接続を具象登録し起動時に購読
    // Registers the space elevator pool and connections by concrete type so their boot-time subscriptions run
    internal static class SpaceElevatorServiceRegistration
    {
        internal static void Register(ServiceCollection services)
        {
            services.AddSingleton<SpaceElevatorCablePool>();
            services.AddSingleton<SpaceElevatorConnectionService>();
        }
    }
}
