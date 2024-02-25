using System;
using Core.Item;
using Cysharp.Threading.Tasks;
using MainGame.Network.Send.SocketUtil;
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

        private static SocketInstanceCreate _socketInstanceCreate;

        public VanillaApi(ServerConnector serverConnector,SinglePlayInterface singlePlayInterface, PlayerConnectionSetting playerConnectionSetting,SocketInstanceCreate socketInstanceCreate)
        {
            _socketInstanceCreate = socketInstanceCreate;
            Event = new VanillaApiEvent(serverConnector, playerConnectionSetting);
            Response = new VanillaApiWithResponse(serverConnector, singlePlayInterface.ItemStackFactory, playerConnectionSetting);
            SendOnly = new VanillaApiSendOnly(serverConnector, singlePlayInterface.ItemStackFactory, playerConnectionSetting);
        }

        //TODO 初期化をちゃんとするようにして最初からstaticアクセスできるようにする
        [Obsolete("初期化をちゃんとするようにして最初からstaticアクセスできるようにする")]
        public static async UniTask WaiteConnection()
        {
            if (_socketInstanceCreate.SocketInstance.Connected)
            {
                return;
            }
            
            await UniTask.WaitUntil(() => _socketInstanceCreate.SocketInstance.Connected);
        }

        public void Initialize() { }
    }
}