using Core.Item;
using MainGame.Network.Settings;
using SinglePlay;
using VContainer.Unity;

namespace Client.Network.API
{
    public class VanillaApi : IInitializable
    {
        public static VanillaApiEvent Event { get; private set; }
        public static VanillaApiWithResponse Response { get; private set; }
        public static VanillaApiSendOnly SendOnly { get; private set; }

        public VanillaApi(ServerConnector serverConnector,SinglePlayInterface singlePlayInterface, PlayerConnectionSetting playerConnectionSetting)
        {
            Event = new VanillaApiEvent(serverConnector, playerConnectionSetting);
            Response = new VanillaApiWithResponse(serverConnector, singlePlayInterface.ItemStackFactory, playerConnectionSetting);
            SendOnly = new VanillaApiSendOnly(serverConnector, singlePlayInterface.ItemStackFactory, playerConnectionSetting);
        }

        public void Initialize() { }
    }
}