using System;
using System.Diagnostics;
using Client.Network.Settings;
using UniRx;
using VContainer.Unity;

namespace Client.Network.API
{
    public class VanillaApi : IInitializable
    {
        private readonly Process _localServerProcess;

        private readonly ServerCommunicator _serverCommunicator;
        public readonly VanillaApiEvent Event;
        public readonly VanillaApiWithResponse Response;
        public readonly VanillaApiSendOnly SendOnly;

        public VanillaApi(PacketExchangeManager packetExchangeManager, PacketSender packetSender, ServerCommunicator serverCommunicator, PlayerConnectionSetting playerConnectionSetting, Process localServerProcess)
        {
            _serverCommunicator = serverCommunicator;
            _localServerProcess = localServerProcess;

            Event = new VanillaApiEvent(packetExchangeManager, playerConnectionSetting);
            Response = new VanillaApiWithResponse(packetExchangeManager, playerConnectionSetting);
            SendOnly = new VanillaApiSendOnly(packetSender, playerConnectionSetting);
        }
        public IObservable<Unit> OnDisconnect => _serverCommunicator.OnDisconnect;

        public void Initialize() { }

        public void Disconnect()
        {
            _serverCommunicator.Close();
            _localServerProcess?.Kill();
        }
    }
}