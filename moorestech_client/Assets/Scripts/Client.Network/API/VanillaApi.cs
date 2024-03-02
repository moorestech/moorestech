using MainGame.Network;
using MainGame.Network.Settings;
using ServerServiceProvider;
using VContainer.Unity;

namespace Client.Network.API
{
    public class VanillaApi : IInitializable
    {
        public static VanillaApiEvent Event { get; private set; }
        public static VanillaApiWithResponse Response { get; private set; }
        public static VanillaApiSendOnly SendOnly { get; private set; }
        
        private static ServerCommunicator _serverCommunicator;
        
        public VanillaApi(PacketExchangeManager packetExchangeManager, PacketSender packetSender,ServerCommunicator serverCommunicator, MoorestechServerServiceProvider moorestechServerServiceProvider, PlayerConnectionSetting playerConnectionSetting)
        {
            _serverCommunicator = serverCommunicator;
            
            Event = new VanillaApiEvent(packetExchangeManager, playerConnectionSetting);
            Response = new VanillaApiWithResponse(packetExchangeManager, moorestechServerServiceProvider.ItemStackFactory, playerConnectionSetting);
            SendOnly = new VanillaApiSendOnly(packetSender, moorestechServerServiceProvider.ItemStackFactory, playerConnectionSetting);
        }

        public void Initialize() { }
        
        public static void Disconnect()
        {
            _serverCommunicator.Close();
        }
    }
}