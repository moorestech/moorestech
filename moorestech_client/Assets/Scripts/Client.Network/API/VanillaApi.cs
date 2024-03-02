using System;
using Cysharp.Threading.Tasks;
using MainGame.Network.Send.SocketUtil;
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

        public VanillaApi(PacketExchangeManager packetExchangeManager,PacketSender packetSender,MoorestechServerServiceProvider moorestechServerServiceProvider, PlayerConnectionSetting playerConnectionSetting)
        {
            Event = new VanillaApiEvent(packetExchangeManager, playerConnectionSetting);
            Response = new VanillaApiWithResponse(packetExchangeManager, moorestechServerServiceProvider.ItemStackFactory, playerConnectionSetting);
            SendOnly = new VanillaApiSendOnly(packetSender, moorestechServerServiceProvider.ItemStackFactory, playerConnectionSetting);
        }

        public void Initialize() { }
    }
}