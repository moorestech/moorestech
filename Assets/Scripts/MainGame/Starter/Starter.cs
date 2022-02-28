using MainGame.Control.Game;
using MainGame.Control.Game.MouseKeyboard;
using MainGame.Control.UI.Command;
using MainGame.Control.UI.Inventory;
using MainGame.Control.UI.Inventory.ItemMove;
using MainGame.Control.UI.UIState;
using MainGame.GameLogic;
using MainGame.GameLogic.Chunk;
using MainGame.GameLogic.Inventory;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Send;
using MainGame.Network.Send.SocketUtil;
using MainGame.UnityView;
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
        
        [Header("ScriptableObjects")]
        [SerializeField] private BlockObjects blockObjects;
        [SerializeField] private ItemImages itemImages;
        
        [Header("InHierarchy")]
        [SerializeField] Camera mainCamera;

        [SerializeField] private GroundPlane groundPlane;

        [SerializeField] private ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore;
        [SerializeField] private HotBarItemView hotBarItemView;
        [SerializeField] private MainInventoryItemView mainInventoryItemView;
        [SerializeField] private CraftingInventoryItemView craftingInventoryItemView;
        [SerializeField] private PlayerInventoryInput playerInventoryInput;
        [SerializeField] private BlockInventoryItemView blockInventoryItemView;
        [SerializeField] private BlockInventoryInput blockInventoryInput;
        [SerializeField] private BlockClickDetect blockClickDetect;
        [SerializeField] private UIStateControl uIStateControl;
        [SerializeField] private CommandUIInput commandUIInput;
        [SerializeField] private MouseGroundClickInput mouseGroundClickInput;
        [SerializeField] private SelectHotBarControl selectHotBarControl;
        [SerializeField] private CraftExecute craftExecute;
        [SerializeField] private PlayerPosition playerPosition;
        
        [SerializeField] private PlayerInventoryEquippedItemImageSet playerInventoryEquippedItemImageSet;
        [SerializeField] private BlockInventoryEquippedItemImageSet blockInventoryEquippedItemImageSet;

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
            builder.Register<INetworkReceivedChunkDataEvent,NetworkReceivedChunkDataEvent>(Lifetime.Singleton);
            builder.Register<IMainInventoryUpdateEvent,MainInventoryUpdateEvent>(Lifetime.Singleton);
            builder.Register<ICraftingInventoryUpdateEvent,CraftingInventoryUpdateEvent>(Lifetime.Singleton);
            builder.Register<IBlockInventoryUpdateEvent,BlockInventoryUpdateEvent>(Lifetime.Singleton);
            
            //パケット送信インスタンス
            builder.RegisterEntryPoint<RequestEventProtocol>(); //イベントは一定時間ごとに送信するのでRegisterEntryPointを使う
            builder.RegisterEntryPoint<SendPlayerPositionProtocolProtocol>(); //プレイヤー位置送信は一定時間ごとに送信するのでRegisterEntryPointを使う
            builder.Register<RequestPlayerInventoryProtocol>(Lifetime.Singleton);
            builder.Register<SendBlockInventoryMoveItemProtocol>(Lifetime.Singleton);
            builder.Register<SendBlockInventoryMainInventoryMoveItemProtocol>(Lifetime.Singleton);
            builder.Register<SendPlaceHotBarBlockProtocol>(Lifetime.Singleton);
            builder.Register<SendMainInventoryMoveItemProtocol>(Lifetime.Singleton);
            builder.Register<RequestBlockInventoryProtocol>(Lifetime.Singleton);
            builder.Register<SendCommandProtocol>(Lifetime.Singleton);
            builder.Register<SendCraftProtocol>(Lifetime.Singleton);
            builder.Register<SendCraftingInventoryMoveItemProtocol>(Lifetime.Singleton);
            builder.Register<SendCraftingInventoryMainInventoryMoveItemProtocol>(Lifetime.Singleton);
            
            
            //データストア、ゲームロジック系
            builder.RegisterEntryPoint<ChunkDataStoreCache>();
            builder.Register<MainInventoryDataCache>(Lifetime.Singleton);
            builder.Register<CraftingInventoryDataCache>(Lifetime.Singleton);
            builder.Register<BlockInventoryDataCache>(Lifetime.Singleton);
            builder.Register<BlockInventoryMainInventoryItemMoveService>(Lifetime.Singleton);
            builder.Register<MainInventoryCraftInventoryItemMoveService>(Lifetime.Singleton);
            
            
            //ScriptableObjectの登録
            builder.RegisterInstance(blockObjects);
            builder.RegisterInstance(itemImages);

            //Hierarchy上にあるcomponent
            builder.RegisterComponent(chunkBlockGameObjectDataStore);
            builder.RegisterComponent(mainCamera);
            builder.RegisterComponent(groundPlane);
            builder.RegisterComponent(mainInventoryItemView);
            builder.RegisterComponent(craftingInventoryItemView);
            builder.RegisterComponent(playerInventoryInput);
            builder.RegisterComponent(blockInventoryItemView);
            builder.RegisterComponent(blockInventoryInput);
            builder.RegisterComponent(mouseGroundClickInput);
            builder.RegisterComponent(playerInventoryEquippedItemImageSet);
            builder.RegisterComponent(blockInventoryEquippedItemImageSet);
            builder.RegisterComponent(uIStateControl);
            builder.RegisterComponent(commandUIInput);
            builder.RegisterComponent(hotBarItemView);
            builder.RegisterComponent(selectHotBarControl);
            builder.RegisterComponent(craftExecute);
            builder.RegisterComponent<IPlayerPosition>(playerPosition);

            builder.RegisterComponent<IBlockClickDetect>(blockClickDetect);
            
            



            //依存関係を解決
            _resolver = builder.Build();
            _resolver.Resolve<ChunkBlockGameObjectDataStore>();
            _resolver.Resolve<MouseGroundClickInput>();
            _resolver.Resolve<MainInventoryItemView>();
            _resolver.Resolve<PlayerInventoryInput>();
            _resolver.Resolve<BlockInventoryItemView>();
            _resolver.Resolve<BlockInventoryInput>();
            _resolver.Resolve<CommandUIInput>();
            _resolver.Resolve<UIStateControl>();

        }

        protected override void OnDestroy()
        {
            _resolver.Dispose();
        }
    }
}
