using Core.Item;
using MainGame.Network.Settings;

namespace Client.Network.API
{
    public class VanillaApi
    {
        public static VanillaApiEvent Event { get; private set; }
        public static VanillaApiWithResponse Response { get; private set; }
        public static VanillaApiSendOnly SendOnly { get; private set; }
        

        public VanillaApi(ServerConnector serverConnector, ItemStackFactory itemStackFactory, PlayerConnectionSetting playerConnectionSetting)
        {
            Event = new VanillaApiEvent(serverConnector, playerConnectionSetting);
            Response = new VanillaApiWithResponse(serverConnector, itemStackFactory, playerConnectionSetting);
            SendOnly = new VanillaApiSendOnly(serverConnector, itemStackFactory, playerConnectionSetting);
        }
    }
}