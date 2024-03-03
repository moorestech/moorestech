using System.Diagnostics;
using Client.Network.API;
using GameConst;
using MainGame.Control.UI.PauseMenu;
using MainGame.Extension;
using MainGame.Presenter.Block;
using MainGame.Presenter.Command;
using MainGame.Presenter.Entity;
using MainGame.Presenter.Inventory;
using MainGame.Presenter.Inventory.Send;
using MainGame.Presenter.MapObject;
using MainGame.Presenter.PauseMenu;
using MainGame.Presenter.Player;
using MainGame.UnityView.Block;
using MainGame.UnityView.Block.StateChange;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.Player;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.Inventory.Sub;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.UI.UIState.UIObject;
using MainGame.UnityView.WorldMapTile;
using ServerServiceProvider;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Client.Starter
{
    /// <summary>
    ///     ゲームの起動と依存解決を行うクラス
    ///     誰かこの最初に全部依存を解決する方法じゃない方法で、いい感じに依存解決できる方法あったら教えてください
    /// </summary>
    public class MainGameStarter : LifetimeScope
    {
        // Hierarchy上にある依存解決が必要なものをまとめたところ
        //TODO regionでちゃんと分類分けしたい

        [SerializeField] private WorldMapTileObject worldMapTileObject;

        [Header("InHierarchy")] [SerializeField]
        private Camera mainCamera;

        [SerializeField] private GroundPlane groundPlane;

        [SerializeField] private ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore;
        [SerializeField] private WorldMapTileGameObjectDataStore worldMapTileGameObjectDataStore;
        [SerializeField] private MapObjectGameObjectDatastore mapObjectGameObjectDatastore;

        [SerializeField] private BlockClickDetect blockClickDetect;
        [SerializeField] private CommandUIInput commandUIInput;
        [SerializeField] private DetectGroundClickToSendBlockPlacePacket detectGroundClickToSendBlockPlacePacket;
        [SerializeField] private HotBarView hotBarView;
        [SerializeField] private PlayerObjectController playerObjectController;
        [SerializeField] private MapObjectGetPresenter mapObjectGetPresenter;

        [SerializeField] private EntityObjectDatastore entityObjectDatastore;

        [SerializeField] private UIStateControl uIStateControl;
        [SerializeField] private PauseMenuObject pauseMenuObject;
        [SerializeField] private DeleteBarObject deleteBarObject;
        [SerializeField] private BlockInventoryView blockInventoryView;
        [SerializeField] private CraftInventoryView craftInventoryView;
        [SerializeField] private PlayerInventoryViewController playerInventoryViewController;

        [SerializeField] private BlockPlacePreview blockPlacePreview;
        [SerializeField] private OreMapTileClickDetect oreMapTileClickDetect;
        [SerializeField] private SaveButton saveButton;
        [SerializeField] private BackToMainMenu backToMainMenu;
        [SerializeField] private NetworkDisconnectPresenter networkDisconnectPresenter;

        [SerializeField] private DisplayEnergizedRange displayEnergizedRange;




        private IObjectResolver _resolver;
        private string IPAddress = ServerConst.LocalServerIp;

        private bool isLocal;
        private Process localServerProcess;

        private int PlayerId = ServerConst.DefaultPlayerId;
        private int Port = ServerConst.LocalServerPort;

        private InitialHandshakeResponse _initialHandshakeResponse;
        
        public void SetInitialHandshakeResponse(InitialHandshakeResponse initialHandshakeResponse)
        {
            _initialHandshakeResponse = initialHandshakeResponse;
        }

        private void Start()
        {
            var builder = new ContainerBuilder();
            //シングルプレイ用のインスタンス
            var singlePlayInterface = new MoorestechServerServiceProvider(ServerConst.ServerDirectory);
            builder.RegisterInstance(singlePlayInterface);
            builder.RegisterInstance(singlePlayInterface.ItemConfig);

            //最初に取得したデータを登録
            builder.RegisterInstance(_initialHandshakeResponse);

            //インベントリのUIコントロール
            builder.Register<LocalPlayerInventoryController>(Lifetime.Singleton);
            builder.Register<ILocalPlayerInventory,LocalPlayerInventory>(Lifetime.Singleton);
            builder.RegisterEntryPoint<NetworkEventInventoryUpdater>();

            //プレゼンターアセンブリ
            builder.RegisterEntryPoint<MachineBlockStateChangeProcessor>();
            builder.RegisterEntryPoint<ChunkDataHandler>();
            builder.RegisterEntryPoint<DeleteBlockDetectToSendPacket>();
            builder.RegisterEntryPoint<PlayerPositionSender>();
            builder.RegisterEntryPoint<BlockStateEventHandler>();


            //UIコントロール
            builder.Register<UIStateDictionary>(Lifetime.Singleton);
            builder.Register<BlockInventoryState>(Lifetime.Singleton);
            builder.Register<GameScreenState>(Lifetime.Singleton);
            builder.Register<PauseMenuState>(Lifetime.Singleton);
            builder.Register<PlayerInventoryState>(Lifetime.Singleton);
            builder.Register<DeleteObjectInventoryState>(Lifetime.Singleton);

            //modからロードしてきたデータ
            builder.Register<WorldMapTileMaterials>(Lifetime.Singleton);

            //ScriptableObjectの登録
            builder.RegisterInstance(worldMapTileObject);

            //Hierarchy上にあるcomponent
            builder.RegisterComponent(chunkBlockGameObjectDataStore);
            builder.RegisterComponent(worldMapTileGameObjectDataStore);
            builder.RegisterComponent(mapObjectGameObjectDatastore);

            builder.RegisterComponent(oreMapTileClickDetect);
            builder.RegisterComponent(mainCamera);
            builder.RegisterComponent(groundPlane);
            builder.RegisterComponent(detectGroundClickToSendBlockPlacePacket);
            builder.RegisterComponent(commandUIInput);
            builder.RegisterComponent(hotBarView);

            builder.RegisterComponent(uIStateControl);
            builder.RegisterComponent(pauseMenuObject);
            builder.RegisterComponent(deleteBarObject);
            builder.RegisterComponent(saveButton);
            builder.RegisterComponent(backToMainMenu);
            builder.RegisterComponent(networkDisconnectPresenter);
            builder.RegisterComponent(mapObjectGetPresenter);

            builder.RegisterComponent(displayEnergizedRange);
            builder.RegisterComponent(entityObjectDatastore);
            builder.RegisterComponent(playerInventoryViewController);
            builder.RegisterComponent(blockInventoryView);
            builder.RegisterComponent(craftInventoryView);


            builder.RegisterComponent<IPlayerObjectController>(playerObjectController);
            builder.RegisterComponent<IBlockClickDetect>(blockClickDetect);
            builder.RegisterComponent<IBlockPlacePreview>(blockPlacePreview);

            builder.RegisterBuildCallback(objectResolver => { });

            //依存関係を解決
            _resolver = builder.Build();
            _resolver.Resolve<ChunkBlockGameObjectDataStore>();
            _resolver.Resolve<DetectGroundClickToSendBlockPlacePacket>();
            _resolver.Resolve<CommandUIInput>();
            _resolver.Resolve<UIStateControl>();
            _resolver.Resolve<DisplayEnergizedRange>();
            _resolver.Resolve<EntityObjectDatastore>();
            _resolver.Resolve<NetworkDisconnectPresenter>();
        }

        protected override void OnDestroy()
        {
            _resolver?.Dispose();
        }
    }
}