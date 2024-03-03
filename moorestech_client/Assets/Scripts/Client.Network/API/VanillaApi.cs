using System;
using System.Diagnostics;
using MainGame.Network;
using MainGame.Network.Settings;
using ServerServiceProvider;
using UniRx;
using VContainer.Unity;

namespace Client.Network.API
{
    public class VanillaApi : IInitializable
    {
        public readonly VanillaApiEvent Event;
        public readonly VanillaApiWithResponse Response;
        public readonly VanillaApiSendOnly SendOnly;
        public IObservable<Unit> OnDisconnect => _serverCommunicator.OnDisconnect;
        
        private readonly ServerCommunicator _serverCommunicator;
        private readonly Process _localServerProcess;
        
        public VanillaApi(PacketExchangeManager packetExchangeManager, PacketSender packetSender,ServerCommunicator serverCommunicator, MoorestechServerServiceProvider moorestechServerServiceProvider, PlayerConnectionSetting playerConnectionSetting, Process localServerProcess)
        {
            _serverCommunicator = serverCommunicator;
            _localServerProcess = localServerProcess;

            Event = new VanillaApiEvent(packetExchangeManager, playerConnectionSetting);
            Response = new VanillaApiWithResponse(packetExchangeManager, moorestechServerServiceProvider.ItemStackFactory, playerConnectionSetting);
            SendOnly = new VanillaApiSendOnly(packetSender, moorestechServerServiceProvider.ItemStackFactory, playerConnectionSetting);
        }

        public void Initialize() { }
        
        public void Disconnect()
        {
            _serverCommunicator.Close();
            _localServerProcess?.Kill();
        }
    }
}