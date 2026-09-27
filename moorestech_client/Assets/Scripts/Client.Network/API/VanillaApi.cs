using System;
using UniRx;
using VContainer.Unity;

namespace Client.Network.API
{
    public class VanillaApi : IInitializable
    {
        private readonly ServerCommunicator _serverCommunicator;
        public readonly IVanillaApiEvent Event;
        public readonly VanillaApiWithResponse Response;
        public readonly VanillaApiSendOnly SendOnly;

        public VanillaApi(PacketExchangeManager packetExchangeManager, PacketSender packetSender, ServerCommunicator serverCommunicator)
        {
            _serverCommunicator = serverCommunicator;

            Event = new VanillaApiEvent(packetExchangeManager);
            Response = new VanillaApiWithResponse(packetExchangeManager);
            SendOnly = new VanillaApiSendOnly(packetSender);
        }
        
        public IObservable<Unit> OnDisconnect => _serverCommunicator.OnDisconnect;
        
        public void Initialize()
        {
        }
        
        public void Disconnect()
        {
            _serverCommunicator.Close();
        }
    }
}