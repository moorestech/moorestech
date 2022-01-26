using System;
using MainGame.GameLogic.Chunk;
using MainGame.GameLogic.Inventory;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Interface;
using MainGame.Network.Interface.Receive;
using MainGame.Network.Interface.Send;
using MainGame.Network.Send;
using MainGame.Network.Send.SocketUtil;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Starter
{
    public class Starter : MonoBehaviour
    {
        private const string DefaultIp = "127.0.0.1";
        private const int DefaultPort = 11564;

        private IObjectResolver _resolver;
        
        void Start()
        {
            var builder = new ContainerBuilder();
            //サーバーに接続するためのインスタンス
            builder.RegisterInstance(new ConnectionServerConfig(DefaultIp,DefaultPort));
            builder.RegisterEntryPoint<ConnectionServer>();
            builder.Register<SocketInstanceCreate, SocketInstanceCreate>(Lifetime.Singleton);
            builder.Register<AllReceivePacketAnalysisService, AllReceivePacketAnalysisService>(Lifetime.Singleton);
            builder.Register<ISocket, SocketObject>(Lifetime.Singleton);
            
            //パケット受け取りイベント
            builder.Register<IChunkUpdateEvent, ChunkUpdateEvent>(Lifetime.Singleton);
            builder.Register<IPlayerInventoryUpdateEvent, PlayerInventoryUpdateEvent>(Lifetime.Singleton);
            
            //パケット送信インスタンス
            builder.Register<IRequestEventProtocol, RequestEventProtocol>(Lifetime.Singleton);
            builder.Register<IRequestPlayerInventoryProtocol, RequestPlayerInventoryProtocol>(Lifetime.Singleton);
            builder.Register<ISendBlockInventoryMoveItemProtocol, SendBlockInventoryMoveItemProtocol>(Lifetime.Singleton);
            builder.Register<ISendBlockInventoryPlayerInventoryMoveItemProtocol, SendBlockInventoryPlayerInventoryMoveItemProtocol>(Lifetime.Singleton);
            builder.Register<ISendPlaceHotBarBlockProtocol, SendPlaceHotBarBlockProtocol>(Lifetime.Singleton);
            builder.Register<ISendPlayerInventoryMoveItemProtocol, SendPlayerInventoryMoveItemProtocol>(Lifetime.Singleton);
            builder.Register<ISendPlayerPositionProtocol, SendPlayerPositionProtocolProtocol>(Lifetime.Singleton);
            
            //データストア
            builder.RegisterEntryPoint<ChunkDataStore>();
            builder.RegisterEntryPoint<InventoryDataStore>();
            
            
            
            //依存関係を解決
            _resolver = builder.Build();
            ;
        }

        private void OnDestroy()
        {
            _resolver.Dispose();
        }
    }
}
