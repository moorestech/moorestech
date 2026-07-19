using Client.Game.InGame.UI.Modal;
using Client.Network.API;
using Client.Network.Settings;

namespace Client.Game.InGame.Context
{
    public class ClientContext
    {
        public static BlockGameObjectPrefabContainer BlockGameObjectPrefabContainer { get; private set; }
        public static ItemImageContainer ItemImageContainer { get; private set; }
        public static BlockImageContainer BlockImageContainer { get; private set; }
        public static TrainCarImageContainer TrainCarImageContainer { get; private set; }
        public static ConnectToolImageContainer ConnectToolImageContainer { get; private set; }
        public static FluidImageContainer FluidImageContainer { get; private set; }
        public static PlayerConnectionSetting PlayerConnectionSetting { get; private set; }
        public static VanillaApi VanillaApi { get; private set; }
        public static ModalManager ModalManager { get; private set; }

        public ClientContext(BlockGameObjectPrefabContainer blockGameObjectPrefabContainer, ItemImageContainer itemImageContainer, BlockImageContainer blockImageContainer, TrainCarImageContainer trainCarImageContainer, ConnectToolImageContainer connectToolImageContainer, FluidImageContainer fluidImageContainer, PlayerConnectionSetting playerConnectionSetting, VanillaApi vanillaApi, ModalManager modalManager)
        {
            BlockGameObjectPrefabContainer = blockGameObjectPrefabContainer;
            ModalManager = modalManager;
            ItemImageContainer = itemImageContainer;
            BlockImageContainer = blockImageContainer;
            TrainCarImageContainer = trainCarImageContainer;
            ConnectToolImageContainer = connectToolImageContainer;
            FluidImageContainer = fluidImageContainer;
            PlayerConnectionSetting = playerConnectionSetting;
            VanillaApi = vanillaApi;
        }
    }
}