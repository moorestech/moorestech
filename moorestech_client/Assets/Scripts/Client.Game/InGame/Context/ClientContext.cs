using Client.Network.API;
using Client.Network.Settings;

namespace Client.Game.InGame.Context
{
    public class ClientContext
    {
        public static BlockGameObjectContainer BlockGameObjectContainer { get; private set; }
        public static ItemImageContainer ItemImageContainer { get; private set; }
        public static PlayerConnectionSetting PlayerConnectionSetting { get; private set; }
        public static VanillaApi VanillaApi { get; private set; }

        public ClientContext(BlockGameObjectContainer blockGameObjectContainer, ItemImageContainer itemImageContainer, PlayerConnectionSetting playerConnectionSetting, VanillaApi vanillaApi)
        {
            BlockGameObjectContainer = blockGameObjectContainer;
            ItemImageContainer = itemImageContainer;
            PlayerConnectionSetting = playerConnectionSetting;
            VanillaApi = vanillaApi;
        }
    }
}