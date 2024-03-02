using MainGame.Network;
using MainGame.Network.Settings;
using ServerServiceProvider;
using VContainer.Unity;

namespace Client.Network.API
{
    public class VanillaApi : IInitializable
    {
        public readonly VanillaApiEvent Event;
        public readonly VanillaApiWithResponse Response;
        public readonly VanillaApiSendOnly SendOnly;
        
        private readonly ServerCommunicator _serverCommunicator;
        
        public VanillaApi(PacketExchangeManager packetExchangeManager, PacketSender packetSender,ServerCommunicator serverCommunicator, MoorestechServerServiceProvider moorestechServerServiceProvider, PlayerConnectionSetting playerConnectionSetting)
        {
            _serverCommunicator = serverCommunicator;
            
            Event = new VanillaApiEvent(packetExchangeManager, playerConnectionSetting);
            Response = new VanillaApiWithResponse(packetExchangeManager, moorestechServerServiceProvider.ItemStackFactory, playerConnectionSetting);
            SendOnly = new VanillaApiSendOnly(packetSender, moorestechServerServiceProvider.ItemStackFactory, playerConnectionSetting);
        }

        public void Initialize() { }
        
        public void Disconnect()
        {
            _serverCommunicator.Close();
        }
    }
}