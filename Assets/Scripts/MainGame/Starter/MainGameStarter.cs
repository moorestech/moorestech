using System.Diagnostics;
using Game.Quest.Interface;
using GameConst;
using MainGame.Control.UI.PauseMenu;
using MainGame.Extension;
using MainGame.ModLoader;
using MainGame.ModLoader.Glb;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Receive;
using MainGame.Network.Send;
using MainGame.Network.Send.SocketUtil;
using MainGame.Network.Settings;
using MainGame.Presenter.Block;
using MainGame.Presenter.Command;
using MainGame.Presenter.Entity;
using MainGame.Presenter.Inventory;
using MainGame.Presenter.Inventory.Receive;
using MainGame.Presenter.Inventory.Send;
using MainGame.Presenter.Loading;
using MainGame.Presenter.MapObject;
using MainGame.Presenter.Player;
using MainGame.Presenter.Quest;
using MainGame.Presenter.Tutorial;
using MainGame.Presenter.Tutorial.ExecutableTutorials;
using MainGame.UnityView.Block;
using MainGame.UnityView.Block.StateChange;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.Game;
using MainGame.UnityView.UI.CraftRecipe;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using MainGame.UnityView.UI.Inventory.View.HotBar;
using MainGame.UnityView.UI.Quest;
using MainGame.UnityView.UI.Tutorial;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.UI.UIState.UIObject;
using MainGame.UnityView.WorldMapTile;
using SinglePlay;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Starter
{
    /// <summary>
    /// ゲームの起動と依存解決を行うクラス
    /// 誰かこの最初に全部依存を解決する方法じゃない方法で、いい感じに依存解決できる方法あったら教えてください
    /// </summary>
    public class MainGameStarter : LifetimeScope
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
        
        
        // Hierarchy上にある依存解決が必要なものをまとめたところ
        //TODO regionでちゃんと分類分けしたい
        
        [SerializeField] private WorldMapTileObject worldMapTileObject;
        
        [Header("InHierarchy")]
        [SerializeField] Camera mainCamera;

        [SerializeField] private GroundPlane groundPlane;
        [SerializeField] private BlockGameObject nothingIndexBlock;

        [SerializeField] private ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore;
        [SerializeField] private WorldMapTileGameObjectDataStore worldMapTileGameObjectDataStore;
        [SerializeField] private MapObjectGameObjectDatastore mapObjectGameObjectDatastore;
        
        [SerializeField] private HotBarItemView hotBarItemView;
        [SerializeField] private BlockClickDetect blockClickDetect;
        [SerializeField] private CommandUIInput commandUIInput;
        [SerializeField] private DetectGroundClickToSendBlockPlacePacket detectGroundClickToSendBlockPlacePacket;
        [SerializeField] private SelectHotBarControl selectHotBarControl;
        [SerializeField] private PlayerPosition playerPosition;
        [SerializeField] private SelectHotBarView selectHotBarView;
        [SerializeField] private ItemRecipeView itemRecipeView;
        [SerializeField] private QuestUI QuestUI;
        [SerializeField] private RecipePlaceButton recipePlaceButton;
        
        [SerializeField] private GrabbedItemImagePresenter grabbedItemImagePresenter;
        [SerializeField] private EntitiesPresenter entitiesPresenter;

        [SerializeField] private UIStateControl uIStateControl;
        [SerializeField] private LoadingFinishDetector loadingFinishDetector;
        [SerializeField] private BlockInventoryObjectCreator blockInventoryObjectCreator;
        [SerializeField] private CraftInventoryObjectCreator craftInventoryObjectCreator;
        [SerializeField] private PauseMenuObject pauseMenuObject;
        [SerializeField] private DeleteBarObject deleteBarObject;
        [SerializeField] private RecipeViewerObject recipeViewerObject;
        [SerializeField] private ItemRecipePresenter itemRecipePresenter;
        [SerializeField] private CraftRecipeItemListViewer craftRecipeItemListViewer;
        [SerializeField] private PlayerInventoryPresenter playerInventoryPresenter;
        [SerializeField] private PlayerInventorySlots playerInventorySlots;
        [SerializeField] private QuestViewerObject questViewerObject;
        [SerializeField] private HighlightRecipeViewerItem highlightRecipeViewerItem;
        [SerializeField] private GameUIHighlight gameUIHighlight;
        
        [SerializeField] private BlockPlacePreview blockPlacePreview;
        [SerializeField] private OreMapTileClickDetect oreMapTileClickDetect;
        [SerializeField] private SaveButton saveButton;
        [SerializeField] private BackToMainMenu backToMainMenu;

        [SerializeField] private DisplayEnergizedRange displayEnergizedRange; 

        [SerializeField] private PlayerInventorySlotsInputControl playerInventorySlotsInputControl;

        [SerializeField] private TutorialExecuter tutorialExecuter;

        void Start()
        {
            var builder = new ContainerBuilder();
            //シングルプレイ用のインスタンス
            var singlePlayInterface = new SinglePlayInterface(ServerConst.ServerDirectory);
            builder.RegisterInstance(singlePlayInterface);
            builder.RegisterInstance(singlePlayInterface.QuestConfig);
            builder.RegisterInstance(singlePlayInterface.ItemConfig);
            builder.RegisterInstance(new ModDirectory(ServerConst.ServerModsDirectory));    
            
            //サーバーに接続するためのインスタンス
            builder.RegisterInstance(new ServerProcessSetting(isLocal,localServerProcess));
            builder.RegisterInstance(new ConnectionServerConfig(IPAddress,Port));
            builder.RegisterInstance(new PlayerConnectionSetting(PlayerId));
            builder.RegisterEntryPoint<ConnectionServer>();
            builder.Register<SocketInstanceCreate, SocketInstanceCreate>(Lifetime.Singleton);
            builder.Register<AllReceivePacketAnalysisService, AllReceivePacketAnalysisService>(Lifetime.Singleton);
            builder.Register<ISocketSender, SocketSender>(Lifetime.Singleton);

            //パケット受け取りイベント
            builder.Register<ReceiveChunkDataEvent>(Lifetime.Singleton);
            builder.Register<ReceiveMainInventoryEvent>(Lifetime.Singleton);
            builder.Register<ReceiveCraftingInventoryEvent>(Lifetime.Singleton);
            builder.Register<ReceiveBlockInventoryEvent>(Lifetime.Singleton);
            builder.Register<ReceiveGrabInventoryEvent>(Lifetime.Singleton);
            builder.Register<ReceiveInitialHandshakeProtocol>(Lifetime.Singleton); //初期接続時に受け取るプロトコル
            builder.Register<ReceiveQuestDataEvent>(Lifetime.Singleton);
            builder.Register<ReceiveEntitiesDataEvent>(Lifetime.Singleton);
            builder.Register<ReceiveBlockStateChangeEvent>(Lifetime.Singleton);
            builder.Register<ReceiveUpdateMapObjectEvent>(Lifetime.Singleton);
            
            //パケット送信インスタンス
            builder.RegisterEntryPoint<RequestEventProtocol>(); //イベントは一定時間ごとに送信するのでRegisterEntryPointを使う
            builder.RegisterEntryPoint<InitialHandshakeProtocol>(); //最初にパケットを送るのでRegisterEntryPointを使う
            builder.Register<SendPlayerPositionProtocolProtocol>(Lifetime.Singleton);
            builder.Register<RequestPlayerInventoryProtocol>(Lifetime.Singleton);
            builder.Register<SendPlaceHotBarBlockProtocol>(Lifetime.Singleton);
            builder.Register<RequestBlockInventoryProtocol>(Lifetime.Singleton);
            builder.Register<SendCommandProtocol>(Lifetime.Singleton);
            builder.Register<SendCraftProtocol>(Lifetime.Singleton);
            builder.Register<SendBlockInventoryOpenCloseControlProtocol>(Lifetime.Singleton);
            builder.Register<SendBlockRemoveProtocol>(Lifetime.Singleton);
            builder.Register<SendMiningProtocol>(Lifetime.Singleton);
            builder.Register<SendSaveProtocol>(Lifetime.Singleton);
            builder.Register<InventoryMoveItemProtocol>(Lifetime.Singleton);
            builder.Register<RequestQuestProgressProtocol>(Lifetime.Singleton);
            builder.Register<SendEarnQuestRewardProtocol>(Lifetime.Singleton);
            builder.Register<SendSetRecipeCraftingInventoryProtocol>(Lifetime.Singleton);
            builder.Register<SendGetMapObjectProtocolProtocol>(Lifetime.Singleton);
            builder.Register<RequestMapObjectDestructionInformationProtocol>(Lifetime.Singleton);

            //インベントリのUIコントロール
            builder.Register<PlayerInventoryViewModel>(Lifetime.Singleton);
            builder.Register<PlayerInventoryViewModelController>(Lifetime.Singleton);
            builder.Register<SubInventoryTypeProvider>(Lifetime.Singleton);
            builder.RegisterComponent(playerInventorySlotsInputControl);
            builder.RegisterComponent(playerInventoryPresenter);
            
            //プレゼンターアセンブリ
            builder.RegisterEntryPoint<MachineBlockStateChangeProcessor>();
            builder.RegisterEntryPoint<ChunkDataPresenter>();
            builder.RegisterEntryPoint<WorldMapTilePresenter>();
            builder.RegisterEntryPoint<DeleteBlockDetectToSendPacket>();
            builder.RegisterEntryPoint<MainInventoryViewPresenter>();
            builder.RegisterEntryPoint<CraftingInventoryViewPresenter>();
            builder.RegisterEntryPoint<BlockInventoryViewPresenter>();
            builder.RegisterEntryPoint<BlockInventoryRequestPacketSend>();
            builder.RegisterEntryPoint<PlayerInventoryRequestPacketSend>();
            builder.RegisterEntryPoint<PlayerInventoryMoveItemPacketSend>();
            builder.RegisterEntryPoint<CraftPacketSend>();
            builder.RegisterEntryPoint<PlayerPositionSender>();
            builder.RegisterEntryPoint<QuestUIPresenter>();
            builder.RegisterEntryPoint<RecipeViewerItemPlacer>();
            builder.RegisterEntryPoint<DirectItemMovePacketSend>();
            builder.RegisterEntryPoint<BlockStateChangePresenter>();
            builder.RegisterEntryPoint<MapObjectPresenter>();
            
            
            //UIコントロール
            builder.Register<UIStateDictionary>(Lifetime.Singleton);
            builder.Register<BlockInventoryState>(Lifetime.Singleton);
            builder.Register<GameScreenState>(Lifetime.Singleton);
            builder.Register<PauseMenuState>(Lifetime.Singleton);
            builder.Register<PlayerInventoryState>(Lifetime.Singleton);
            builder.Register<DeleteObjectInventoryState>(Lifetime.Singleton);
            builder.Register<BlockPlaceState>(Lifetime.Singleton);
            builder.Register<RecipeViewState>(Lifetime.Singleton);
            builder.Register<QuestViewerState>(Lifetime.Singleton);

            //modからロードしてきたデータ
            builder.Register<ItemImages>(Lifetime.Singleton);
            builder.Register<WorldMapTileMaterials>(Lifetime.Singleton);
            builder.Register<BlockGameObjectFactory>(Lifetime.Singleton);
            
            //チュートリアル関係
            builder.RegisterComponent(tutorialExecuter);
            builder.Register<_0_IronMiningTutorial>(Lifetime.Singleton);
            builder.Register<_1_MinerCraftTutorial>(Lifetime.Singleton);
            

            //ScriptableObjectの登録
            builder.RegisterInstance(worldMapTileObject);

            //Hierarchy上にあるcomponent
            builder.RegisterComponent(nothingIndexBlock);
            
            builder.RegisterComponent(chunkBlockGameObjectDataStore);
            builder.RegisterComponent(worldMapTileGameObjectDataStore);
            builder.RegisterComponent(mapObjectGameObjectDatastore);
            
            builder.RegisterComponent(oreMapTileClickDetect);
            builder.RegisterComponent(mainCamera);
            builder.RegisterComponent(groundPlane);
            builder.RegisterComponent(detectGroundClickToSendBlockPlacePacket);
            builder.RegisterComponent(grabbedItemImagePresenter);
            builder.RegisterComponent(commandUIInput);
            builder.RegisterComponent(hotBarItemView);
            builder.RegisterComponent(selectHotBarControl);
            builder.RegisterComponent(selectHotBarView);
            builder.RegisterComponent(itemRecipeView);
            builder.RegisterComponent(QuestUI);
            builder.RegisterComponent(highlightRecipeViewerItem);
            builder.RegisterComponent(gameUIHighlight);
            
            builder.RegisterComponent(uIStateControl);
            builder.RegisterComponent(loadingFinishDetector);
            builder.RegisterComponent(craftInventoryObjectCreator);
            builder.RegisterComponent(blockInventoryObjectCreator);
            builder.RegisterComponent(pauseMenuObject);
            builder.RegisterComponent(deleteBarObject);
            builder.RegisterComponent(recipeViewerObject);
            builder.RegisterComponent(saveButton);
            builder.RegisterComponent(backToMainMenu);
            builder.RegisterComponent(itemRecipePresenter);
            builder.RegisterComponent(craftRecipeItemListViewer);
            builder.RegisterComponent(playerInventorySlots);
            builder.RegisterComponent(questViewerObject);
            builder.RegisterComponent(recipePlaceButton);
            
            builder.RegisterComponent(displayEnergizedRange);
            builder.RegisterComponent(entitiesPresenter);
            
            
            builder.RegisterComponent<IPlayerPosition>(playerPosition);
            builder.RegisterComponent<IBlockClickDetect>(blockClickDetect);
            builder.RegisterComponent<IBlockPlacePreview>(blockPlacePreview);
            
            
            



            //依存関係を解決
            _resolver = builder.Build();
            _resolver.Resolve<ChunkBlockGameObjectDataStore>();
            _resolver.Resolve<DetectGroundClickToSendBlockPlacePacket>();
            _resolver.Resolve<CommandUIInput>();
            _resolver.Resolve<UIStateControl>();
            _resolver.Resolve<LoadingFinishDetector>();
            _resolver.Resolve<DisplayEnergizedRange>();
            _resolver.Resolve<EntitiesPresenter>();
        }

        protected override void OnDestroy()
        {
            _resolver.Dispose();
        }
    }
}
