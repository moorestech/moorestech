using System.Diagnostics;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Control;
using Client.Game.InGame.CraftTree.TreeView;
using Client.Game.InGame.Electric;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Environment;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Mining;
using Client.Game.InGame.Player;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.Presenter.Player;
using Client.Game.InGame.Skit;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Block.Research;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.InGame.UnlockState;
using Client.Game.InGame.World;
using Client.Game.InGame.Train;
using Client.Game.InGame.UI.Inventory.Craft;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Game.Skit;
using Client.Network.API;
using Client.Skit.Skit;
using Client.Skit.UI;
using Game.UnlockState;
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
        
        [SerializeField] private GameStateController gameStateController;
        [SerializeField] private BlockGameObjectDataStore blockGameObjectDataStore;
        [SerializeField] private MapObjectGameObjectDatastore mapObjectGameObjectDatastore;
        [SerializeField] private EnvironmentRoot environmentRoot;
        
        [SerializeField] private HotBarView hotBarView;
        [SerializeField] private MapObjectMiningController mapObjectMiningController;
        [SerializeField] private PlayerSystemContainer playerSystemContainer;
        
        [SerializeField] private EntityObjectDatastore entityObjectDatastore;
        
        [SerializeField] private UIStateControl uIStateControl;
        [SerializeField] private PauseMenuObject pauseMenuObject;
        [SerializeField] private DeleteBarObject deleteBarObject;
        [SerializeField] private PlayerInventoryViewController playerInventoryViewController;
        [SerializeField] private CraftInventoryView craftInventoryView;
        [SerializeField] private MachineRecipeView machineRecipeView;
        [SerializeField] private RecipeViewerView recipeViewerView;
        [SerializeField] private ItemListView itemListView;
        [SerializeField] private RecipeTabView recipeTabView;
        [SerializeField] private CraftTreeViewManager craftTreeViewManager;
        [SerializeField] private ChallengeListView challengeListView;
        [SerializeField] private ResearchTreeViewManager researchTreeViewManager;

        [SerializeField] private MapObjectPin mapObjectPin;
        [SerializeField] private UIHighlightTutorialManager uiHighlightTutorialManager;
        [SerializeField] private KeyControlTutorialManager keyControlTutorialManager;
        [SerializeField] private ItemViewHighLightTutorialManager itemViewHighLightTutorialManager;
        
        [SerializeField] private PlacementPreviewBlockGameObjectController previewBlockController;
        [SerializeField] private RailConnectPreviewObject railConnectPreviewObject;
        [SerializeField] private SaveButton saveButton;
        [SerializeField] private BackToMainMenu backToMainMenu;
        [SerializeField] private NetworkDisconnectPresenter networkDisconnectPresenter;
        [SerializeField] private ChallengeManager challengeManager;
        
        [SerializeField] private TrainRailObjectManager trainRailObjectManager;
        [SerializeField] private TrainCarPreviewController trainCarObjectPreviewController;
        
        [SerializeField] private SkitManager skitManager;
        [SerializeField] private SkitUI skitUI;
        [SerializeField] private BackgroundSkitManager backgroundSkitManager;
        
        [SerializeField] private DisplayEnergizedRange displayEnergizedRange;
        
        [SerializeField] private InGameCameraController inGameCameraController;
        
        
        private IObjectResolver _resolver;
        private string IPAddress = ServerConst.LocalServerIp;
        
        private bool isLocal;
        private Process localServerProcess;
        
        private int PlayerId = ServerConst.DefaultPlayerId;
        private int Port = ServerConst.LocalServerPort;
        
        protected override void OnDestroy()
        {
            _resolver?.Dispose();
        }
        
        public IObjectResolver StartGame(InitialHandshakeResponse initialHandshakeResponse)
        {
            var builder = new ContainerBuilder();
            
            CameraManager.Initialize();
            
            //PureC#のインスタンスを登録
            // Register pure C# instances
            //最初に取得したデータを登録
            // register initial data
            builder.RegisterInstance(initialHandshakeResponse);
            
            //インベントリのUIコントロール
            // register inventory UI control
            builder.Register<LocalPlayerInventoryController>(Lifetime.Singleton);
            builder.Register<ILocalPlayerInventory, LocalPlayerInventory>(Lifetime.Singleton);
            builder.RegisterEntryPoint<NetworkEventInventoryUpdater>();
            
            //プレゼンターアセンブリ
            // register presenter assembly
            builder.RegisterEntryPoint<CommonMachineBlockStateChangeProcessor>();
            builder.RegisterEntryPoint<WorldDataHandler>();
            builder.RegisterEntryPoint<PlayerPositionSender>();
            builder.RegisterEntryPoint<SkitFireManager>();
            
            // 設置システム
            // register placement system
            builder.Register<CommonBlockPlaceSystem>(Lifetime.Singleton);
            builder.Register<ITrainCarPlacementDetector, TrainCarPlacementDetector>(Lifetime.Singleton);
            builder.Register<TrainCarPlaceSystem>(Lifetime.Singleton);
            builder.Register<TrainRailPlaceSystem>(Lifetime.Singleton);
            builder.Register<TrainRailConnectSystem>(Lifetime.Singleton);
            builder.Register<PlaceSystemStateController>(Lifetime.Singleton);
            builder.Register<PlaceSystemSelector>(Lifetime.Singleton);
            
            
            //UIコントロール
            // register UI control
            builder.Register<UIStateDictionary>(Lifetime.Singleton);
            builder.Register<SubInventoryState>(Lifetime.Singleton);
            builder.Register<GameScreenState>(Lifetime.Singleton);
            builder.Register<PauseMenuState>(Lifetime.Singleton);
            builder.Register<PlayerInventoryState>(Lifetime.Singleton);
            builder.Register<DeleteBlockState>(Lifetime.Singleton);
            builder.Register<SkitState>(Lifetime.Singleton);
            builder.Register<PlaceBlockState>(Lifetime.Singleton);
            builder.Register<ChallengeListState>(Lifetime.Singleton);
            builder.Register<ResearchTreeState>(Lifetime.Singleton);
            builder.Register<ItemRecipeViewerDataContainer>(Lifetime.Singleton);
            builder.Register<GameScreenSubInventoryInteractService>(Lifetime.Singleton);
            
            // スキット関連
            // register skit related
            var skitActionContext = new SkitActionContext();
            builder.RegisterInstance<ISkitActionContext>(skitActionContext);
            builder.RegisterInstance<ISkitActionController>(skitActionContext);
            
            // その他インスタンス
            // register other instance
            builder.Register<TutorialManager>(Lifetime.Singleton);
            builder.Register<IGameUnlockStateData, ClientGameUnlockStateData>(Lifetime.Singleton);
            
            
            //Hierarchy上にあるcomponent
            // register component on hierarchy
            builder.RegisterComponent(gameStateController);
            builder.RegisterComponent(blockGameObjectDataStore);
            builder.RegisterComponent(mapObjectGameObjectDatastore);
            builder.RegisterComponent(environmentRoot);
            
            builder.RegisterComponent(mainCamera);
            builder.RegisterComponent(hotBarView);
            
            builder.RegisterComponent(uIStateControl);
            builder.RegisterComponent(pauseMenuObject);
            builder.RegisterComponent(deleteBarObject);
            builder.RegisterComponent(saveButton);
            builder.RegisterComponent(backToMainMenu);
            builder.RegisterComponent(networkDisconnectPresenter);
            builder.RegisterComponent(mapObjectMiningController);
            
            builder.RegisterComponent(displayEnergizedRange);
            builder.RegisterComponent(entityObjectDatastore);
            builder.RegisterComponent(playerInventoryViewController);
            builder.RegisterComponent(challengeManager);
            builder.RegisterComponent(craftInventoryView);
            builder.RegisterComponent(machineRecipeView);
            builder.RegisterComponent(recipeViewerView);
            builder.RegisterComponent(itemListView);
            builder.RegisterComponent(recipeTabView);
            builder.RegisterComponent(craftTreeViewManager);
            builder.RegisterComponent(challengeListView);
            builder.RegisterComponent(researchTreeViewManager);

            builder.RegisterComponent<IMapObjectPin>(mapObjectPin);
            builder.RegisterComponent(uiHighlightTutorialManager);
            builder.RegisterComponent(keyControlTutorialManager);
            builder.RegisterComponent(itemViewHighLightTutorialManager);
            
            builder.RegisterComponent(playerSystemContainer);
            builder.RegisterComponent(skitManager).As<IInitializable>();
            builder.RegisterComponent(skitUI);
            builder.RegisterComponent(backgroundSkitManager);
            
            builder.RegisterComponent(inGameCameraController).As<IInitializable>();
            
            builder.RegisterComponent<IPlacementPreviewBlockGameObjectController>(previewBlockController);
            builder.RegisterComponent(railConnectPreviewObject);
            builder.RegisterComponent(trainRailObjectManager);
            builder.RegisterComponent(trainCarObjectPreviewController);
            
            builder.RegisterBuildCallback(objectResolver => { });
            
            //依存関係を解決
            // resolve dependency
            _resolver = builder.Build();
            _resolver.Resolve<BlockGameObjectDataStore>();
            _resolver.Resolve<UIStateControl>();
            _resolver.Resolve<DisplayEnergizedRange>();
            _resolver.Resolve<EntityObjectDatastore>();
            _resolver.Resolve<ChallengeManager>();
            _resolver.Resolve<PlayerSystemContainer>();
            _resolver.Resolve<SkitUI>();
            
            return _resolver;
        }
    }
}
