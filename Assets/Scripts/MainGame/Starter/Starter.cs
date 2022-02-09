using MainGame.Control.UI.Inventory;
using MainGame.GameLogic;
using MainGame.GameLogic.Chunk;
using MainGame.GameLogic.Event;
using MainGame.GameLogic.Inventory;
using MainGame.GameLogic.Send;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Interface;
using MainGame.Network.Interface.Receive;
using MainGame.Network.Interface.Send;
using MainGame.Network.Send;
using MainGame.Network.Send.SocketUtil;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.ControllerInput.Event;
using MainGame.UnityView.ControllerInput.MouseKeyboard;
using MainGame.UnityView.Interface;
using MainGame.UnityView.Interface.Chunk;
using MainGame.UnityView.Interface.PlayerInput;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using UnityEngine.Serialization;
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

        [SerializeField] private EquippedItemViewControl equippedItemViewControl;
        [SerializeField] private PlayerInventoryItemView playerInventoryItemView;
        [SerializeField] private MouseInventoryInput mouseInventoryInput;
        [SerializeField] private ItemImages itemImages;
        
        void Start()
        {
            var builder = new ContainerBuilder();
            //サーバーに接続するためのインスタンス
            builder.RegisterInstance(new ConnectionServerConfig(DefaultIp,DefaultPort));
            builder.RegisterInstance(new ConnectionPlayerSetting(PlayerId));
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
            
            //GameLogicのPresenterの作成
            builder.RegisterEntryPoint<BlockPlaceEventToSendProtocol>();
            builder.Register<IPlayerInventoryViewUpdateEvent,PlayerInventoryViewUpdateEvent>(Lifetime.Singleton);
            builder.Register<IPlayerInventoryItemMove,PlayerInventoryItemMove>(Lifetime.Singleton);
            
            //データストア
            builder.RegisterEntryPoint<ChunkDataStoreCache>();
            builder.Register<IBlockUpdateEvent, BlockUpdateEvent>(Lifetime.Singleton);
            builder.Register<InventoryDataStoreCache>(Lifetime.Singleton);
            
            //ScriptableObjectの登録
            builder.RegisterInstance(blockObjects);
            
            
            //Viewのイベント登録
            builder.Register<IBlockPlaceEvent, BlockPlaceEvent>(Lifetime.Singleton);
            
            
            //MonoBehaviourのprefabの登録
            builder.RegisterComponentInNewPrefab(chunkBlockGameObjectDataStore, Lifetime.Singleton);
            
            //MonoBehaviourのインスタンスの登録
            builder.RegisterComponentOnNewGameObject<MouseGroundClickInput>(Lifetime.Singleton);
            
            //Hierarchy上にあるcomponent
            builder.RegisterComponent(mainCamera);
            builder.RegisterComponent(groundPlane);
            builder.RegisterComponent(equippedItemViewControl);
            builder.RegisterComponent(playerInventoryItemView);
            builder.RegisterComponent(mouseInventoryInput);
            builder.RegisterInstance(itemImages);
            
            



            //依存関係を解決
            _resolver = builder.Build();
            _resolver.Resolve<ChunkBlockGameObjectDataStore>();
            _resolver.Resolve<MouseGroundClickInput>();
            _resolver.Resolve<PlayerInventoryItemView>();
            _resolver.Resolve<EquippedItemViewControl>();
            _resolver.Resolve<MouseInventoryInput>();
            _resolver.Resolve<IPlayerInventoryItemMove>();

        }

        protected override void OnDestroy()
        {
            _resolver.Dispose();
        }
    }
}
