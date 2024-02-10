using System.Diagnostics;
using Client.Localization;
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
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.Inventory.Sub;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.UI.UIState.UIObject;
using MainGame.UnityView.WorldMapTile;
using SinglePlay;
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
        [SerializeField] private BlockGameObject nothingIndexBlock;

        [SerializeField] private ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore;
        [SerializeField] private WorldMapTileGameObjectDataStore worldMapTileGameObjectDataStore;
        [SerializeField] private MapObjectGameObjectDatastore mapObjectGameObjectDatastore;

        [SerializeField] private BlockClickDetect blockClickDetect;
        [SerializeField] private CommandUIInput commandUIInput;
        [SerializeField] private DetectGroundClickToSendBlockPlacePacket detectGroundClickToSendBlockPlacePacket;
        [SerializeField] private HotBarView hotBarView;
        [SerializeField] private PlayerObjectController playerObjectController;
        [SerializeField] private MapObjectGetPresenter mapObjectGetPresenter;

        [SerializeField] private EntitiesPresenter entitiesPresenter;

        [SerializeField] private UIStateControl uIStateControl;
        [SerializeField] private LoadingFinishDetector loadingFinishDetector;
        [SerializeField] private PauseMenuObject pauseMenuObject;
        [SerializeField] private DeleteBarObject deleteBarObject;
        [SerializeField] private BlockInventoryView blockInventoryView;
        [SerializeField] private CraftInventoryView craftInventoryView;
        [SerializeField] private PlayerInventoryController playerInventoryController;

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

        private void Start()
        {
            Debug.Log(Localize.Get("start"));


            var builder = new ContainerBuilder();
            //シングルプレイ用のインスタンス
            var singlePlayInterface = new SinglePlayInterface(ServerConst.ServerDirectory);
            builder.RegisterInstance(singlePlayInterface);
            builder.RegisterInstance(singlePlayInterface.ItemConfig);
            builder.RegisterInstance(new ModDirectory(ServerConst.ServerModsDirectory));

            //サーバーに接続するためのインスタンス
            builder.RegisterInstance(new ServerProcessSetting(isLocal, localServerProcess));
            builder.RegisterInstance(new ConnectionServerConfig(IPAddress, Port));
            builder.RegisterInstance(new PlayerConnectionSetting(PlayerId));
            builder.Register<ConnectionServer>(Lifetime.Scoped);
            builder.Register<SocketInstanceCreate, SocketInstanceCreate>(Lifetime.Singleton);
            builder.Register<ISocketSender, SocketSender>(Lifetime.Singleton);

            //パケット受け取りイベント
            builder.Register<ReceiveChunkDataEvent>(Lifetime.Singleton);
            builder.Register<ReceiveInitialHandshakeProtocol>(Lifetime.Singleton); //初期接続時に受け取るプロトコル
            builder.Register<ReceiveEntitiesDataEvent>(Lifetime.Singleton);
            builder.Register<ReceiveBlockStateChangeEvent>(Lifetime.Singleton);
            builder.Register<ReceiveUpdateMapObjectEvent>(Lifetime.Singleton);

            //パケット送信インスタンス
            builder.RegisterEntryPoint<RequestEventProtocol>(); //イベントは一定時間ごとに送信するのでRegisterEntryPointを使う
            builder.RegisterEntryPoint<InitialHandshakeProtocol>(); //最初にパケットを送るのでRegisterEntryPointを使う

            builder.Register<SendPlayerPositionProtocolProtocol>(Lifetime.Singleton);
            builder.Register<RequestPlayerInventoryProtocol>(Lifetime.Singleton);
            builder.Register<SendPlaceHotBarBlockProtocol>(Lifetime.Singleton);
            builder.Register<SendCommandProtocol>(Lifetime.Singleton);
            builder.Register<SendBlockInventoryOpenCloseControlProtocol>(Lifetime.Singleton);
            builder.Register<SendBlockRemoveProtocol>(Lifetime.Singleton);
            builder.Register<SendMiningProtocol>(Lifetime.Singleton);
            builder.Register<SendSaveProtocol>(Lifetime.Singleton);
            builder.Register<InventoryMoveItemProtocol>(Lifetime.Singleton);
            builder.Register<SendGetMapObjectProtocolProtocol>(Lifetime.Singleton);
            builder.Register<SendOneClickCraftProtocol>(Lifetime.Singleton);

            //インベントリのUIコントロール
            builder.Register<LocalPlayerInventoryController>(Lifetime.Singleton);
            builder.Register<ILocalPlayerInventory,LocalPlayerInventory>(Lifetime.Singleton);

            //プレゼンターアセンブリ
            builder.RegisterEntryPoint<MachineBlockStateChangeProcessor>();
            builder.RegisterEntryPoint<ChunkDataPresenter>();
            builder.RegisterEntryPoint<WorldMapTilePresenter>();
            builder.RegisterEntryPoint<DeleteBlockDetectToSendPacket>();
            builder.RegisterEntryPoint<PlayerInventoryRequestPacketSend>();
            builder.RegisterEntryPoint<PlayerPositionSender>();
            builder.RegisterEntryPoint<BlockStateChangePresenter>();


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
            builder.RegisterComponent(nothingIndexBlock);

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
            builder.RegisterComponent(entitiesPresenter);
            builder.RegisterComponent(playerInventoryController);
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
            _resolver.Resolve<EntitiesPresenter>();
            _resolver.Resolve<ConnectionServer>();
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