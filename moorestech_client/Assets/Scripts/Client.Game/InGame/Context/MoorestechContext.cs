using Client.Network.API;
using Client.Network.Settings;
using ServerServiceProvider;

namespace Client.Game.InGame.Context
{
    public class MoorestechContext
    {
        public MoorestechContext(BlockGameObjectContainer blockGameObjectContainer, ItemImageContainer itemImageContainer, PlayerConnectionSetting playerConnectionSetting, VanillaApi vanillaApi, MoorestechServerServiceProvider serverServices)
        {
            BlockGameObjectContainer = blockGameObjectContainer;
            ItemImageContainer = itemImageContainer;
            PlayerConnectionSetting = playerConnectionSetting;
            VanillaApi = vanillaApi;
            ServerServices = serverServices;
        }
        public static BlockGameObjectContainer BlockGameObjectContainer { get; private set; }
        public static ItemImageContainer ItemImageContainer { get; private set; }
        public static PlayerConnectionSetting PlayerConnectionSetting { get; private set; }
        public static VanillaApi VanillaApi { get; private set; }

        public static MoorestechServerServiceProvider ServerServices { get; private set; }
    }
}