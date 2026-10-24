using Client.Game.InGame.CleanRoom;
using VContainer;

namespace Client.Starter.Registration
{
    // 部屋の照準表示を登録
    // Registers the room aim display
    internal static class CleanRoomUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<CleanRoomBoundaryHoverService>(Lifetime.Singleton);
        }
    }
}
