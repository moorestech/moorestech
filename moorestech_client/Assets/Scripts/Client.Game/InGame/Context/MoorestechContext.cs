using Client.Network.API;
using MainGame.Network.Settings;
using MainGame.UnityView.Item;
using ServerServiceProvider;

namespace Client.Game.Context
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