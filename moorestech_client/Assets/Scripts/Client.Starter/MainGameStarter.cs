using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Environment;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Mining;
using Client.Game.InGame.Player;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.Riding;
using Client.Game.InGame.Skit;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Block.Research;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.InGame.Train.View;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.Inventory.Craft;
using Client.Game.Skit;
using Client.Network.API;
using Client.Skit.Skit;
using Client.Skit.UI;
using Client.Starter.Registration;
using CommandForgeGenerator.Command;
using UnityEngine;
using UnityEngine.Serialization;
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
        [Header("InHierarchy")] [SerializeField]
        private Camera mainCamera;
        [SerializeField] private GameStateController gameStateController;
        [SerializeField] private BlockGameObjectDataStore blockGameObjectDataStore;
        [SerializeField] private MapObjectGameObjectDatastore mapObjectGameObjectDatastore;
        [SerializeField] private OutcropGameObjectDatastore outcropGameObjectDatastore;
        [SerializeField] private EnvironmentRoot environmentRoot;

        // 地形の実行時構築はDIの外（Finalizer）で走るため、マウント先だけを読み取り専用で公開する
        // Runtime terrain construction runs outside DI in the finalizer, so only read access to the mount point is exposed
        public EnvironmentRoot EnvironmentRoot => environmentRoot;
        
        // 対象非依存へrename済み。prefabに残る旧キーからそのまま引き継ぐ
        // Renamed to a target-agnostic name; the old key still in the prefab carries over as is
        [FormerlySerializedAs("mapObjectMiningController")]
        [SerializeField] private MiningController miningController;
        [SerializeField] private PlayerSystemContainer playerSystemContainer;
        
        [SerializeField] private EntityObjectDatastore entityObjectDatastore;
        [SerializeField] private TrainCarObjectDatastore trainCarObjectDatastore;
        
        [SerializeField] private UIStateControl uIStateControl;
        [SerializeField] private PauseMenuObject pauseMenuObject;
        [SerializeField] private DeleteBarObject deleteBarObject;
        [SerializeField] private BuildMenuView buildMenuView;
        [SerializeField] private BlueprintNameInputView blueprintNameInputView;
        [SerializeField] private PlayerInventoryViewController playerInventoryViewController;
        [SerializeField] private CraftInventoryView craftInventoryView;
        [SerializeField] private MachineRecipeView machineRecipeView;
        [SerializeField] private RecipeViewerView recipeViewerView;
        [SerializeField] private ItemListView itemListView;
        [SerializeField] private RecipeTabView recipeTabView;
        [SerializeField] private ChallengeListView challengeListView;
        [SerializeField] private ResearchTreeViewManager researchTreeViewManager;

        [SerializeField] private MapObjectPin mapObjectPin;
        [SerializeField] private VeinPin veinPin;
        [SerializeField] private UIHighlightTutorialManager uiHighlightTutorialManager;
        [SerializeField] private KeyControlTutorialManager keyControlTutorialManager;
        [SerializeField] private ItemViewHighLightTutorialManager itemViewHighLightTutorialManager;
        [SerializeField] private BlockPlacePreviewTutorialManager blockPlacePreviewTutorialManager;
        [SerializeField] private UiDragGuideTutorialManager uiDragGuideTutorialManager;
        [SerializeField] private VeinRestrictedPlacementTutorialManager veinRestrictedPlacementTutorialManager;
        [SerializeField] private RelativeBlockPlacePreviewTutorialManager relativeBlockPlacePreviewTutorialManager;
        
        [SerializeField] private PlacementPreviewBlockGameObjectController previewBlockController;
        [SerializeField] private RailConnectPreviewObject railConnectPreviewObject;
        [SerializeField] private SaveButton saveButton;
        [FormerlySerializedAs("backToMainMenu")]
        [SerializeField] private SaveAndQuitPresenter saveAndQuitPresenter;
        [SerializeField] private NetworkDisconnectPresenter networkDisconnectPresenter;
        [SerializeField] private ChallengeManager challengeManager;
        
        [SerializeField] private TrainRailObjectManager trainRailObjectManager;
        [SerializeField] private TrainCarPreviewController trainCarObjectPreviewController;
        
        [SerializeField] private SkitManager skitManager;
        [SerializeField] private SkitUI skitUI;
        [SerializeField] private BackgroundSkitManager backgroundSkitManager;
        
        [SerializeField] private InGameCameraController inGameCameraController;
        
        
        private IObjectResolver _resolver;

        protected override void OnDestroy()
        {
            _resolver?.Dispose();
        }
        
        public IObjectResolver StartGame(InitialHandshakeResponse initialHandshakeResponse)
        {
            var builder = new ContainerBuilder();

            CameraManager.Initialize();

            MainGameModelRegistration.Register(builder, initialHandshakeResponse);
            MainGameInteractionRegistration.Register(builder, initialHandshakeResponse);

            
            //Hierarchy上にあるcomponent
            // register component on hierarchy
            builder.RegisterComponent(gameStateController);
            builder.RegisterComponent(blockGameObjectDataStore);
            builder.RegisterComponent(mapObjectGameObjectDatastore).AsSelf().As<IMapObjectPinTargetSource>().As<ISkitWorldObjectControl>();
            builder.Register<MapObjectNearFieldWaitTarget>(Lifetime.Singleton).As<IInitialEventApplyWaitTarget>();
            builder.RegisterComponent(outcropGameObjectDatastore).AsSelf().As<IInitialEventApplyWaitTarget>().As<ISkitWorldObjectControl>();
            builder.RegisterComponent(environmentRoot);
            
            builder.RegisterComponent(mainCamera);

            builder.RegisterComponent(uIStateControl);
            builder.RegisterComponent(pauseMenuObject);
            builder.RegisterComponent(deleteBarObject);
            builder.RegisterComponent(buildMenuView).AsSelf().As<IBuildMenuView>();
            builder.RegisterComponent(blueprintNameInputView);
            builder.RegisterComponent(saveButton);
            builder.RegisterComponent(saveAndQuitPresenter);
            builder.RegisterComponent(networkDisconnectPresenter);
            builder.RegisterComponent(miningController);
            
            builder.RegisterComponent(entityObjectDatastore);
            builder.RegisterComponent(trainCarObjectDatastore).AsSelf().As<ISkitWorldObjectControl>();
            builder.RegisterComponent(playerInventoryViewController);
            builder.RegisterComponent(challengeManager);
            builder.RegisterComponent(craftInventoryView);
            builder.RegisterComponent(machineRecipeView);
            builder.RegisterComponent(recipeViewerView);
            builder.RegisterComponent(itemListView);
            builder.RegisterComponent(recipeTabView);
            builder.RegisterComponent(challengeListView);
            builder.RegisterComponent(researchTreeViewManager);

            builder.RegisterComponent(mapObjectPin).AsSelf().As<ITutorialWorldPin>().As<ITutorialViewManager>();
            builder.RegisterComponent(veinPin).AsSelf().As<ITutorialWorldPin>().As<ITutorialViewManager>();
            builder.RegisterComponent(uiHighlightTutorialManager).AsSelf().As<ITutorialViewManager>();
            builder.RegisterComponent(keyControlTutorialManager).AsSelf().As<ITutorialViewManager>();
            builder.RegisterComponent(itemViewHighLightTutorialManager).AsSelf().As<ITutorialViewManager>();
            builder.RegisterComponent(blockPlacePreviewTutorialManager).AsSelf().As<ITutorialViewManager>();
            builder.RegisterComponent(uiDragGuideTutorialManager).AsSelf().As<ITutorialViewManager>();
            builder.RegisterComponent(veinRestrictedPlacementTutorialManager).AsSelf().As<ITutorialViewManager>();
            builder.RegisterComponent(relativeBlockPlacePreviewTutorialManager).AsSelf().As<ITutorialViewManager>();
            
            builder.RegisterComponent(playerSystemContainer);
            builder.RegisterComponent(skitManager).As<IInitializable>();
            builder.RegisterComponent(skitUI);
            builder.RegisterComponent(backgroundSkitManager);
            
            builder.RegisterComponent(inGameCameraController).As<IInitializable>();
            
            builder.RegisterComponent<IPlacementPreviewBlockGameObjectController>(previewBlockController);
            builder.RegisterComponent(railConnectPreviewObject);
            builder.RegisterComponent(trainRailObjectManager).AsSelf().As<ISkitWorldObjectControl>();
            builder.RegisterComponent(trainCarObjectPreviewController);
            
            builder.RegisterBuildCallback(objectResolver => { });
            
            _resolver = builder.Build();
            MainGameContainerActivation.ResolveRequiredServices(_resolver);

            return _resolver;
        }

        // 初期snapshot適用後に呼ぶ
        // Call after the initial snapshot is applied
        public void RestoreLoginState(InitialHandshakeResponse init)
        {
            MainGameContainerActivation.RestoreLoginState(_resolver, init);
        }
    }
}
