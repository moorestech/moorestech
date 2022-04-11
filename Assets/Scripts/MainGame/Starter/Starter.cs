using System.Diagnostics;
using GameConst;
using MainGame.Control.UI.Inventory;
using MainGame.Control.UI.PauseMenu;
using MainGame.Control.UI.UIState.UIObject;
using MainGame.Control.UI.UIState.UIState;
using MainGame.GameLogic.Inventory;
using MainGame.Model.DataStore.Chunk;
using MainGame.Model.DataStore.Inventory;
using MainGame.Model.Network.Send;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Send;
using MainGame.Network.Send.SocketUtil;
using MainGame.Network.Settings;
using MainGame.Presenter;
using MainGame.Presenter.Command;
using MainGame.Presenter.Inventory;
using MainGame.Presenter.ItemMove;
using MainGame.UnityView.Block;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.Game;
using MainGame.UnityView.UI.CraftRecipe;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.WorldMapTile;
using SinglePlay;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Starter
{
    public class Starter : LifetimeScope
    {
        private string IPAddress = ServerConst.LocalServerIp;
        private int Port = ServerConst.LocalServerPort;
        
        private int PlayerId = ServerConst.DefaultPlayerId;
        
        private bool isLocal = false;
        private Process localServerProcess = null;

        public void SetProperty(MainGameStartProprieties proprieties)
        {
            IPAddress = proprieties.serverIp;
            Port = proprieties.serverPort;
            isLocal = proprieties.isLocal;
            
            PlayerId = proprieties.playerId;
            localServerProcess = proprieties.localServerProcess;
        }
        

        private IObjectResolver _resolver;
        
        [Header("ScriptableObjects")]
        [SerializeField] private BlockObjects blockObjects;
        [SerializeField] private ItemImages itemImages;
        [SerializeField] private WorldMapTileObjects worldMapTileObjects;
        
        [Header("InHierarchy")]
        [SerializeField] Camera mainCamera;

        [SerializeField] private GroundPlane groundPlane;

        [SerializeField] private ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore;
        [SerializeField] private WorldMapTileGameObjectDataStore worldMapTileGameObjectDataStore;
        
        [SerializeField] private HotBarItemView hotBarItemView;
        [SerializeField] private MainInventoryItemView mainInventoryItemView;
        [SerializeField] private CraftingInventoryItemView craftingInventoryItemView;
        [SerializeField] private PlayerInventoryInput playerInventoryInput;
        [SerializeField] private BlockInventoryItemView blockInventoryItemView;
        [SerializeField] private BlockInventoryInput blockInventoryInput;
        [SerializeField] private BlockClickDetect blockClickDetect;
        [SerializeField] private CommandUIInput commandUIInput;
        [SerializeField] private DetectGroundClickToSendBlockPlacePacket detectGroundClickToSendBlockPlacePacket;
        [SerializeField] private SelectHotBarControl selectHotBarControl;
        [SerializeField] private CraftExecute craftExecute;
        [SerializeField] private PlayerPosition playerPosition;
        [SerializeField] private SelectHotBarView selectHotBarView;
        [SerializeField] private ItemRecipeView itemRecipeView;
        
        [SerializeField] private PlayerInventoryEquippedItemImageSet playerInventoryEquippedItemImageSet;
        [SerializeField] private BlockInventoryEquippedItemImageSet blockInventoryEquippedItemImageSet;
        
        [SerializeField] private UIStateControl uIStateControl;
        [SerializeField] private BlockInventoryObject blockInventoryObject;
        [SerializeField] private PlayerInventoryObject playerInventoryObject;
        [SerializeField] private PauseMenuObject pauseMenuObject;
        [SerializeField] private DeleteBarObject deleteBarObject;
        [SerializeField] private RecipeViewerObject recipeViewerObject;
        [SerializeField] private ItemListViewer itemListViewer;
        [SerializeField] private ItemRecipePresenter itemRecipePresenter;
        
        [SerializeField] private BlockPlacePreview blockPlacePreview;
        [SerializeField] private OreMapTileClickDetect oreMapTileClickDetect;
        [SerializeField] private SaveButton saveButton;
        [SerializeField] private BackToMainMenu backToMainMenu;

        void Start()
        {
            var builder = new ContainerBuilder();
            //シングルプレイ用のインスタンス
            builder.RegisterInstance(new SinglePlayInterface(ServerConst.ServerConfigDirectory));
            
            //サーバーに接続するためのインスタンス
            builder.RegisterInstance(new ServerProcessSetting(isLocal,localServerProcess));
            builder.RegisterInstance(new ConnectionServerConfig(IPAddress,Port));
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
            builder.Register<SendBlockInventoryOpenCloseControl>(Lifetime.Singleton);
            builder.Register<SendBlockRemoveProtocol>(Lifetime.Singleton);
            builder.Register<SendMiningProtocol>(Lifetime.Singleton);
            builder.Register<SendSaveProtocol>(Lifetime.Singleton);
            
            
            //データストア、ゲームロジック系
            builder.RegisterEntryPoint<ChunkDataStoreCache>();
            builder.RegisterEntryPoint<WorldMapTilePresenter>();
            builder.RegisterEntryPoint<DeleteBlockDetectToSendPacket>();
            builder.Register<MainInventoryDataCache>(Lifetime.Singleton);
            builder.Register<CraftingInventoryDataCache>(Lifetime.Singleton);
            builder.Register<BlockInventoryDataCache>(Lifetime.Singleton);
            builder.Register<BlockInventoryMainInventoryItemMoveService>(Lifetime.Singleton);
            builder.Register<MainInventoryCraftInventoryItemMoveService>(Lifetime.Singleton);
            
            //インプット
            builder.Register<MoorestechInputSettings>(Lifetime.Singleton);
            
            //UIコントロール
            builder.Register<UIStateDictionary>(Lifetime.Singleton);
            builder.Register<BlockInventoryState>(Lifetime.Singleton);
            builder.Register<GameScreenState>(Lifetime.Singleton);
            builder.Register<PauseMenuState>(Lifetime.Singleton);
            builder.Register<PlayerInventoryState>(Lifetime.Singleton);
            builder.Register<DeleteObjectInventoryState>(Lifetime.Singleton);
            builder.Register<BlockPlaceState>(Lifetime.Singleton);
            builder.Register<RecipeViewState>(Lifetime.Singleton);


            //ScriptableObjectの登録
            builder.RegisterInstance(blockObjects);
            builder.RegisterInstance(itemImages);
            builder.RegisterInstance(worldMapTileObjects);

            //Hierarchy上にあるcomponent
            builder.RegisterComponent(chunkBlockGameObjectDataStore);
            builder.RegisterComponent(worldMapTileGameObjectDataStore);
            
            builder.RegisterComponent(oreMapTileClickDetect);
            builder.RegisterComponent(mainCamera);
            builder.RegisterComponent(groundPlane);
            builder.RegisterComponent(mainInventoryItemView);
            builder.RegisterComponent(craftingInventoryItemView);
            builder.RegisterComponent(playerInventoryInput);
            builder.RegisterComponent(blockInventoryItemView);
            builder.RegisterComponent(blockInventoryInput);
            builder.RegisterComponent(detectGroundClickToSendBlockPlacePacket);
            builder.RegisterComponent(playerInventoryEquippedItemImageSet);
            builder.RegisterComponent(blockInventoryEquippedItemImageSet);
            builder.RegisterComponent(commandUIInput);
            builder.RegisterComponent(hotBarItemView);
            builder.RegisterComponent(selectHotBarControl);
            builder.RegisterComponent(craftExecute);
            builder.RegisterComponent(selectHotBarView);
            builder.RegisterComponent(itemRecipeView);
            
            builder.RegisterComponent(uIStateControl);
            builder.RegisterComponent(playerInventoryObject);
            builder.RegisterComponent(blockInventoryObject);
            builder.RegisterComponent(pauseMenuObject);
            builder.RegisterComponent(deleteBarObject);
            builder.RegisterComponent(itemListViewer);
            builder.RegisterComponent(recipeViewerObject);
            builder.RegisterComponent(saveButton);
            builder.RegisterComponent(backToMainMenu);
            builder.RegisterComponent(itemRecipePresenter);
            
            
            builder.RegisterComponent<IPlayerPosition>(playerPosition);
            builder.RegisterComponent<IBlockClickDetect>(blockClickDetect);
            builder.RegisterComponent<IBlockPlacePreview>(blockPlacePreview);
            
            



            //依存関係を解決
            _resolver = builder.Build();
            _resolver.Resolve<ChunkBlockGameObjectDataStore>();
            _resolver.Resolve<DetectGroundClickToSendBlockPlacePacket>();
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
