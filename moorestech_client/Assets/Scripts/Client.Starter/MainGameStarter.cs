using System.Diagnostics;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.Control;
using Client.Game.InGame.Electric;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Environment;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Mining;
using Client.Game.InGame.Player;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.Skit;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Block.Research;
using Client.Game.InGame.UI.Inventory.Craft;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Network.API;
using Client.Skit.UI;
using Game.PlayerRiding.Interface;
using Game.Train.Unit;
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
        // Collect dependencies on hierarchy here
        //TODO regionでちゃんと分類分けしたい
        // TODO classify this properly with regions
        
        [Header("InHierarchy")] [SerializeField]
        internal Camera mainCamera;
        
        [SerializeField] internal GameStateController gameStateController;
        [SerializeField] internal BlockGameObjectDataStore blockGameObjectDataStore;
        [SerializeField] internal MapObjectGameObjectDatastore mapObjectGameObjectDatastore;
        [SerializeField] internal EnvironmentRoot environmentRoot;
        
        [SerializeField] internal HotBarView hotBarView;
        [SerializeField] internal MapObjectMiningController mapObjectMiningController;
        [SerializeField] internal PlayerSystemContainer playerSystemContainer;
        
        [SerializeField] internal EntityObjectDatastore entityObjectDatastore;
        [SerializeField] internal TrainCarObjectDatastore trainCarObjectDatastore;
        
        [SerializeField] internal UIStateControl uIStateControl;
        [SerializeField] internal PauseMenuObject pauseMenuObject;
        [SerializeField] internal DeleteBarObject deleteBarObject;
        [SerializeField] internal BuildMenuView buildMenuView;
        [SerializeField] internal BlueprintNameInputView blueprintNameInputView;
        [SerializeField] internal PlayerInventoryViewController playerInventoryViewController;
        [SerializeField] internal CraftInventoryView craftInventoryView;
        [SerializeField] internal MachineRecipeView machineRecipeView;
        [SerializeField] internal RecipeViewerView recipeViewerView;
        [SerializeField] internal ItemListView itemListView;
        [SerializeField] internal RecipeTabView recipeTabView;
        [SerializeField] internal ChallengeListView challengeListView;
        [SerializeField] internal ResearchTreeViewManager researchTreeViewManager;

        [SerializeField] internal MapObjectPin mapObjectPin;
        [SerializeField] internal UIHighlightTutorialManager uiHighlightTutorialManager;
        [SerializeField] internal KeyControlTutorialManager keyControlTutorialManager;
        [SerializeField] internal ItemViewHighLightTutorialManager itemViewHighLightTutorialManager;
        [SerializeField] internal BlockPlacePreviewTutorialManager blockPlacePreviewTutorialManager;
        
        [SerializeField] internal PlacementPreviewBlockGameObjectController previewBlockController;
        [SerializeField] internal RailConnectPreviewObject railConnectPreviewObject;
        [SerializeField] internal SaveButton saveButton;
        [SerializeField] internal BackToMainMenu backToMainMenu;
        [SerializeField] internal NetworkDisconnectPresenter networkDisconnectPresenter;
        [SerializeField] internal ChallengeManager challengeManager;
        
        [SerializeField] internal TrainRailObjectManager trainRailObjectManager;
        [SerializeField] internal TrainCarPreviewController trainCarObjectPreviewController;
        
        [SerializeField] internal SkitManager skitManager;
        [SerializeField] internal SkitUI skitUI;
        [SerializeField] internal BackgroundSkitManager backgroundSkitManager;
        
        [SerializeField] internal DisplayEnergizedRange displayEnergizedRange;
        
        [SerializeField] internal InGameCameraController inGameCameraController;
        
        
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
            
            // PureC#のインスタンスを登録
            // Register pure C# instances
            // 最初に取得したデータを登録
            // register initial data
            builder.RegisterInstance(initialHandshakeResponse);
            
            // PureC#系のDI登録（インベントリ・ネットワーク・設置・UIState・列車等）
            // Pure C# DI registrations (inventory, network, placement, UI state, train, etc.)
            MainGameStarterServiceRegistration.RegisterInventory(builder);
            MainGameStarterServiceRegistration.RegisterNetworkHandlers(builder);
            MainGameStarterServiceRegistration.RegisterPlacementSystems(builder);
            MainGameStarterServiceRegistration.RegisterViewMode(builder);
            MainGameStarterServiceRegistration.RegisterUiStates(builder);
            MainGameStarterServiceRegistration.RegisterPlayerStates(builder);
            MainGameStarterServiceRegistration.RegisterSkit(builder);
            MainGameStarterServiceRegistration.RegisterClientServices(builder);

            //Hierarchy上にあるcomponentを登録
            // Register hierarchy components
            MainGameStarterHierarchyRegistration.RegisterHierarchyComponents(builder, this);

            builder.RegisterBuildCallback(objectResolver => { });
            
            //依存関係を解決
            // resolve dependency
            _resolver = builder.Build();
            _resolver.Resolve<BlockGameObjectDataStore>();
            _resolver.Resolve<UIStateControl>();
            _resolver.Resolve<DisplayEnergizedRange>();
            _resolver.Resolve<EntityObjectDatastore>();
            _resolver.Resolve<TrainCarObjectDatastore>();
            _resolver.Resolve<ChallengeManager>();
            _resolver.Resolve<PlayerSystemContainer>();
            _resolver.Resolve<SkitUI>();

            return _resolver;
        }

        // 初期snapshot適用後に呼ぶ
        // Call after the initial snapshot is applied
        public void RestoreLoginState(InitialHandshakeResponse init)
        {
            var context = new UITransitContext(UIStateEnum.GameScreen);
            var uiState = UIStateEnum.GameScreen;
            
            if (init.RidingTarget != null && init.RidingTarget.RidableType == RidableType.TrainCar)
            {
                var request = new InitialRideTrainCarRequest(new TrainCarInstanceId(init.RidingTarget.TrainCarInstanceId), init.RidingSeatIndex);
                var container = UITransitContextContainer.Create(request);
                
                context = new UITransitContext(UIStateEnum.TrainHUDScreen, container);
                uiState = UIStateEnum.TrainHUDScreen;
                
            }
            
            _resolver.Resolve<UIStateControl>().Initialize(uiState, context);
        }
    }
}
