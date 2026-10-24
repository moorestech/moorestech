using Client.Game.InGame.CleanRoom;
using VContainer;

namespace Client.Starter.Registration
{
    // クリーンルームの照準表示（ツールチップ・漏れマーカー）を登録する
    // Registers the clean-room aim display: tooltip and leak markers
    internal static class CleanRoomUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<CleanRoomBoundaryHoverService>(Lifetime.Singleton);
        }
    }
}
