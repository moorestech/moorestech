using Game.SpaceElevator;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Boot.Composition
{
    // 宇宙エレベーターのプール・接続を登録する。起動時に購読を張るので具象型で登録する
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
