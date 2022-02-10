using MainGame.Control.Game;
using MainGame.Control.Game.MouseKeyboard;
using MainGame.Control.UI.Control;
using MainGame.Control.UI.Inventory;
using MainGame.GameLogic;
using MainGame.GameLogic.Chunk;
using MainGame.GameLogic.Inventory;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Send;
using MainGame.Network.Send.SocketUtil;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Starter
{
    public class Starter : LifetimeScope
    {
        private const string DefaultIp = "127.0.0.1";
        private const int DefaultPort = 11564;
        private const int PlayerId = 1;
        

        private IObjectResolver _resolver;
        [SerializeField] ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore;
        [SerializeField] BlockObjects blockObjects;
        
        [Header("InHierarchy")]
        [SerializeField] Camera mainCamera;

        [SerializeField] private GroundPlane groundPlane;

        [SerializeField] private PlayerInventoryItemView playerInventoryItemView;
        [SerializeField] private PlayerInventoryInput playerInventoryInput;
        [SerializeField] private BlockInventoryItemView blockInventoryItemView;
        [SerializeField] private BlockInventoryInput blockInventoryInput;
        [SerializeField] private BlockClickDetect blockClickDetect;
        [SerializeField] private ItemImages itemImages;
        [SerializeField] private UIStateControl uIStateControl; 

        void Start()
        {
            var builder = new ContainerBuilder();
            //サーバーに接続するためのインスタンス
            builder.RegisterInstance(new ConnectionServerConfig(DefaultIp,DefaultPort));
            builder.RegisterInstance(new PlayerConnectionSetting(PlayerId));
            builder.RegisterEntryPoint<ConnectionServer>();
            builder.Register<SocketInstanceCreate, SocketInstanceCreate>(Lifetime.Singleton);
            builder.Register<AllReceivePacketAnalysisService, AllReceivePacketAnalysisService>(Lifetime.Singleton);
            builder.Register<ISocket, SocketObject>(Lifetime.Singleton);

            //パケット受け取りイベント
            builder.Register<ChunkUpdateEvent>(Lifetime.Singleton);
            builder.Register<PlayerInventoryUpdateEvent>(Lifetime.Singleton);
            
            //パケット送信インスタンス
            builder.Register<RequestEventProtocol>(Lifetime.Singleton);
            builder.Register<RequestPlayerInventoryProtocol>(Lifetime.Singleton);
            builder.Register<SendBlockInventoryMoveItemProtocol>(Lifetime.Singleton);
            builder.Register<SendBlockInventoryPlayerInventoryMoveItemProtocol>(Lifetime.Singleton);
            builder.Register<SendPlaceHotBarBlockProtocol>(Lifetime.Singleton);
            builder.Register<SendPlayerInventoryMoveItemProtocol>(Lifetime.Singleton);
            builder.Register<SendPlayerPositionProtocolProtocol>(Lifetime.Singleton);
            builder.Register<RequestBlockInventoryProtocol>(Lifetime.Singleton);
            
            
            //データストア
            builder.RegisterEntryPoint<ChunkDataStoreCache>();
            builder.Register<PlayerInventoryDataCache>(Lifetime.Singleton);
            
            //ScriptableObjectの登録
            builder.RegisterInstance(blockObjects);
            
            //MonoBehaviourのprefabの登録
            builder.RegisterComponentInNewPrefab(chunkBlockGameObjectDataStore, Lifetime.Singleton);
            
            //MonoBehaviourのインスタンスの登録
            builder.RegisterComponentOnNewGameObject<MouseGroundClickInput>(Lifetime.Singleton);
            
            //Hierarchy上にあるcomponent
            builder.RegisterComponent(mainCamera);
            builder.RegisterComponent(groundPlane);
            builder.RegisterComponent(playerInventoryItemView);
            builder.RegisterComponent(playerInventoryInput);
            builder.RegisterComponent(blockInventoryItemView);
            builder.RegisterComponent(blockInventoryInput);
            builder.RegisterInstance(itemImages);
            builder.RegisterInstance(uIStateControl);

            builder.RegisterComponent<IBlockClickDetect>(blockClickDetect);
            
            



            //依存関係を解決
            _resolver = builder.Build();
            _resolver.Resolve<ChunkBlockGameObjectDataStore>();
            _resolver.Resolve<MouseGroundClickInput>();
            _resolver.Resolve<PlayerInventoryItemView>();
            _resolver.Resolve<PlayerInventoryInput>();
            _resolver.Resolve<BlockInventoryItemView>();
            _resolver.Resolve<BlockInventoryInput>();
            _resolver.Resolve<UIStateControl>();

        }

        protected override void OnDestroy()
        {
            _resolver.Dispose();
        }
    }
}
