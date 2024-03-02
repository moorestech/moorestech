using System.Diagnostics;
using Client.Game.Context;
using Client.Localization;
using Client.Network.API;
using GameConst;
using MainGame.Control.UI.PauseMenu;
using MainGame.Extension;
using MainGame.ModLoader;
using MainGame.ModLoader.Glb;
using MainGame.Network;
using MainGame.Network.Send;
using MainGame.Network.Send.SocketUtil;
using MainGame.Network.Settings;
using MainGame.Presenter.Block;
using MainGame.Presenter.Command;
using MainGame.Presenter.Entity;
using MainGame.Presenter.Inventory;
using MainGame.Presenter.Inventory.Send;
using MainGame.Presenter.Loading;
using MainGame.Presenter.MapObject;
using MainGame.Presenter.PauseMenu;
using MainGame.Presenter.Player;
using MainGame.UnityView.Block;
using MainGame.UnityView.Block.StateChange;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.Item;
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
using Debug = UnityEngine.Debug;

namespace MainGame.Starter
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
        [SerializeField] private LoadingFinishDetector loadingFinishDetector;
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

        public void ResolveGame(ServerCommunicator serverCommunicator)
        {
            Debug.Log(Localize.Get("start"));


            var builder = new ContainerBuilder();
            //シングルプレイ用のインスタンス
            var singlePlayInterface = new MoorestechServerServiceProvider(ServerConst.ServerDirectory);
            builder.RegisterInstance(singlePlayInterface);
            builder.RegisterInstance(singlePlayInterface.ItemConfig);
            builder.RegisterInstance(new ModDirectory(ServerConst.ServerModsDirectory));

            //サーバーに接続するためのインスタンス
            builder.RegisterInstance(serverCommunicator);
            builder.RegisterInstance(new PlayerConnectionSetting(PlayerId));
            builder.RegisterEntryPoint<VanillaApi>();
            builder.Register<PacketExchangeManager>(Lifetime.Singleton);
            builder.Register<PacketSender>(Lifetime.Singleton);
            builder.Register<ServerCommunicator>(Lifetime.Singleton);

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
            builder.Register<ItemImageContainer>(Lifetime.Singleton);
            builder.Register<WorldMapTileMaterials>(Lifetime.Singleton);
            builder.Register<BlockGameObjectContainer>(Lifetime.Singleton);

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
            builder.RegisterComponent(loadingFinishDetector);
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
            _resolver.Resolve<LoadingFinishDetector>();
            _resolver.Resolve<DisplayEnergizedRange>();
            _resolver.Resolve<EntityObjectDatastore>();
            _resolver.Resolve<ServerCommunicator>();
            _resolver.Resolve<NetworkDisconnectPresenter>();
        }

        protected override void OnDestroy()
        {
            _resolver.Dispose();
        }

        public void SetProperty(MainGameStartProprieties proprieties)
        {
            IPAddress = proprieties.serverIp;
            Port = proprieties.serverPort;
            isLocal = proprieties.isLocal;

            PlayerId = proprieties.playerId;
            localServerProcess = proprieties.localServerProcess;
        }
    }
}