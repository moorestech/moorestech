using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Core.Item;
using Cysharp.Threading.Tasks;
using Game.World.Interface.DataStore;
using MainGame.Network.Send;
using MainGame.Network.Settings;
using MainGame.Presenter.Block;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.NewApi
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