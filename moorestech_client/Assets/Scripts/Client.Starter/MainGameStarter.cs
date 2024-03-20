using System.Diagnostics;
using Client.Game.BlockSystem;
using Client.Game.BlockSystem.StateChange;
using Client.Game.Map.MapObject;
using Client.Game.UI.UIState;
using Client.Game.UI.UIState.UIObject;
using Client.Network.API;
using GameConst;
using MainGame.Control.UI.PauseMenu;
using MainGame.Extension;
using MainGame.Presenter.Block;
using MainGame.Presenter.Command;
using MainGame.Presenter.Entity;
using MainGame.Presenter.PauseMenu;
using MainGame.Presenter.Player;
using MainGame.UnityView.Block;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.Player;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.Inventory.Sub;
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

        [Header("InHierarchy")] [SerializeField]
        private Camera mainCamera;

        [SerializeField] private ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore;
        [SerializeField] private MapObjectGameObjectDatastore mapObjectGameObjectDatastore;

        [SerializeField] private CommandUIInput commandUIInput;
        [SerializeField] private BlockPlaceSystem blockPlaceSystem;
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

        public void StartGame(InitialHandshakeResponse initialHandshakeResponse)
        {
            var builder = new ContainerBuilder();

            //最初に取得したデータを登録
            builder.RegisterInstance(initialHandshakeResponse);

            //インベントリのUIコントロール
            builder.Register<LocalPlayerInventoryController>(Lifetime.Singleton);
            builder.Register<ILocalPlayerInventory,LocalPlayerInventory>(Lifetime.Singleton);
            builder.RegisterEntryPoint<NetworkEventInventoryUpdater>();

            //プレゼンターアセンブリ
            builder.RegisterEntryPoint<MachineBlockStateChangeProcessor>();
            builder.RegisterEntryPoint<ChunkDataHandler>();
            builder.RegisterEntryPoint<PlayerPositionSender>();
            builder.RegisterEntryPoint<BlockStateEventHandler>();


            //UIコントロール
            builder.Register<UIStateDictionary>(Lifetime.Singleton);
            builder.Register<BlockInventoryState>(Lifetime.Singleton);
            builder.Register<GameScreenState>(Lifetime.Singleton);
            builder.Register<PauseMenuState>(Lifetime.Singleton);
            builder.Register<PlayerInventoryState>(Lifetime.Singleton);
            builder.Register<DeleteBlockState>(Lifetime.Singleton);


            //Hierarchy上にあるcomponent
            builder.RegisterComponent(chunkBlockGameObjectDataStore);
            builder.RegisterComponent(mapObjectGameObjectDatastore);

            builder.RegisterComponent(mainCamera);
            builder.RegisterComponent(blockPlaceSystem);
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
            builder.RegisterComponent<IBlockPlacePreview>(blockPlacePreview);

            builder.RegisterBuildCallback(objectResolver => { });

            //依存関係を解決
            _resolver = builder.Build();
            _resolver.Resolve<ChunkBlockGameObjectDataStore>();
            _resolver.Resolve<BlockPlaceSystem>();
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