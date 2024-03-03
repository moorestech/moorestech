using Client.Network.API;
using MainGame.Network.Settings;
using MainGame.UnityView.Item;

namespace Client.Game.Context
{
    public class MoorestechContext
    {
        public static BlockGameObjectContainer BlockGameObjectContainer { get; private set; }
        public static ItemImageContainer ItemImageContainer { get; private set; }
        public static PlayerConnectionSetting PlayerConnectionSetting { get; private set; }
        public static VanillaApi VanillaApi { get; private set; }
        
        public MoorestechContext(BlockGameObjectContainer blockGameObjectContainer, ItemImageContainer itemImageContainer, PlayerConnectionSetting playerConnectionSetting, VanillaApi vanillaApi)
        {
            BlockGameObjectContainer = blockGameObjectContainer;
            ItemImageContainer = itemImageContainer;
            PlayerConnectionSetting = playerConnectionSetting;
            VanillaApi = vanillaApi;
        }
    }
}