using System;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.Block;
using Client.Game.InGame.ColliderStreaming;
using Client.Game.InGame.ColliderStreaming.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Game.PlacementTarget;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Environment;
using Client.Game.InGame.Construction;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Mining;
using Client.Game.InGame.Player;
using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Player.StateController.State;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.Presenter.Player;
using Client.Game.InGame.Riding;
using Client.Game.InGame.Skit;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Block.Research;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.InGame.UnlockState;
using Client.Game.InGame.World;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.DebugView;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.Inventory.Craft;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Game.InGame.UI.UIState.State.Hotbar;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Game.Skit;
using Client.Skit.Context;
using Client.Network.API;
using Client.Skit.Skit;
using Client.Skit.UI;
using CommandForgeGenerator.Command;
using Core.Item.Interface;
using Game.Context;
using Game.PlayerRiding.Interface;
using Game.Train.Unit;
using Game.Construction;
using Game.UnlockState;
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
        //TODO regionでちゃんと分類分けしたい
        // TODO classify this properly with regions
        
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
            
            // PureC#のインスタンスを登録
            // Register pure C# instances
            // 最初に取得したデータを登録
            // register initial data
            builder.RegisterInstance(initialHandshakeResponse);
            
            //インベントリのUIコントロール
            // register inventory UI control
            builder.RegisterInstance(ClientContext.VanillaApi.Event);
            builder.Register<LocalPlayerInventoryController>(Lifetime.Singleton);
            builder.Register<ILocalPlayerInventory, LocalPlayerInventory>(Lifetime.Singleton);
            builder.RegisterEntryPoint<NetworkEventInventoryUpdater>();
            // ホットバー割当モデルと更新購読
            // Hotbar's 9-slot assignment-reference model and its update-event subscription
            builder.Register<ClientHotbarDatastore>(Lifetime.Singleton);
            builder.RegisterEntryPoint<HotbarNetworkEventHandler>();
            // 残り設置数モデルと更新購読
            // Remaining-placement model and its update-event subscription
            builder.Register<ClientRemainingPlacementCountDatastore>(Lifetime.Singleton).AsSelf().As<IRemainingPlacementCountReader>();
            builder.RegisterEntryPoint<RemainingPlacementCountEventHandler>();
            // 財布への問い合わせ窓口。クライアント側の判断はすべてここを通す
            // The wallet's query window; every client-side judgement goes through it
            builder.Register<ConstructionWalletQuery>(Lifetime.Singleton);
            // 装備モデルと、その選択に追従する手持ち3Dモデル
            // Equipment model and the held 3D model that follows its selection
            builder.Register<LocalPlayerEquipment>(Lifetime.Singleton);
            builder.RegisterEntryPoint<EquipmentHeldItemModel>();
            // スタックレベルの変更系はDI注入のみで公開する
            // Expose stack level mutation only through DI injection
            builder.RegisterInstance(ServerContext.GetService<IItemStackLevelUnlocker>());
            builder.RegisterEntryPoint<ItemStackLevelEventHandler>();
            
            //プレゼンターアセンブリ
            // register presenter assembly
            builder.RegisterEntryPoint<CommonMachineBlockStateChangeProcessor>();
            builder.RegisterEntryPoint<WorldDataHandler>();
            // コライダーの距離カリング（汎用マネージャ＋ブロック登録サービス）
            // Collider distance culling (generic manager + block register service)
            builder.Register<ColliderDistanceCullingManager>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.RegisterEntryPoint<BlockColliderCullingRegisterService>();
            builder.RegisterEntryPoint<PlayerPositionSender>().AsSelf();
            builder.RegisterEntryPoint<SkitFireManager>();
            builder.RegisterEntryPoint<RailGraphCacheNetworkHandler>();
            builder.RegisterEntryPoint<RailGraphConnectionNetworkHandler>();
            builder.RegisterEntryPoint<TrainUnitSnapshotEventNetworkHandler>();
            builder.RegisterEntryPoint<TrainUnitTickDiffBundleEventNetworkHandler>();
            builder.RegisterEntryPoint<TrainFullSnapshotEventNetworkHandler>().AsSelf();
            
            // 設置システム
            // register placement system
            builder.Register<CommonBlockPlaceSystem>(Lifetime.Singleton);
            builder.Register<BeltConveyorPlaceSystem>(Lifetime.Singleton);
            builder.Register<ITrainCarPlacementDetector, TrainCarPlacementDetector>(Lifetime.Singleton);
            builder.Register<TrainCarPlaceSystem>(Lifetime.Singleton);
            builder.Register<TrainRailPlaceSystem>(Lifetime.Singleton);
            builder.Register<TrainRailConnectSystem>(Lifetime.Singleton);
            builder.Register<GearChainPoleConnectSystem>(Lifetime.Singleton);
            builder.Register<ElectricWireConnectSystem>(Lifetime.Singleton);
            builder.Register<PlaceSystemStateController>(Lifetime.Singleton);
            builder.Register<PlaceSystemSelector>(Lifetime.Singleton);
            builder.Register<ClientBlueprintLibrary>(Lifetime.Singleton);
            // 設置プレビュー中の鉱脈範囲表示。設置側はIMapVeinRangeView越しにプッシュするだけ
            // Vein range view during placement preview; the placement side only pushes through IMapVeinRangeView
            builder.Register<MapVeinRangeViewService>(Lifetime.Singleton).As<IMapVeinRangeView>();
            builder.Register<PlacementTargetCatalog>(Lifetime.Singleton);
            builder.Register<BlueprintPasteSystem>(Lifetime.Singleton);
            builder.Register<BlueprintCopySystem>(Lifetime.Singleton);
            // 設置対象解決・キー判別・タップ振り分け
            // Placement-target resolution, digit-key press classification, and hotbar tap routing
            builder.Register<PlacementTargetResolver>(Lifetime.Singleton);
            builder.Register<HotbarKeyInput>(Lifetime.Singleton);
            builder.Register<HotbarTapInputService>(Lifetime.Singleton);
            builder.Register<HotbarSelectionReconciler>(Lifetime.Singleton);

            // UI非依存の視点モード処理
            // UI-independent view-mode processing
            builder.Register<IPlayerViewApplier, PlayerViewApplier>(Lifetime.Singleton);
            builder.Register<IPlayerCameraInteractionApplier, PlayerCameraInteractionApplier>(Lifetime.Singleton);
            builder.Register<PlayerViewModeController>(Lifetime.Singleton).AsSelf().As<IStartable>().As<ITickable>();
            builder.Register<UiStateCameraPolicyService>(Lifetime.Singleton);


            //UIコントロール
            // register UI control
            builder.Register<UIStateDictionary>(Lifetime.Singleton);
            builder.Register<SubInventoryState>(Lifetime.Singleton);
            builder.Register<GameScreenState>(Lifetime.Singleton);
            builder.Register<PauseMenuState>(Lifetime.Singleton);
            builder.Register<PlayerInventoryState>(Lifetime.Singleton);
            builder.Register<DeleteObjectState>(Lifetime.Singleton);
            builder.Register<SkitState>(Lifetime.Singleton);
            builder.Register<PlaceBlockState>(Lifetime.Singleton);
            builder.Register<ChallengeListState>(Lifetime.Singleton);
            builder.Register<ResearchTreeState>(Lifetime.Singleton);
            builder.Register<DebugBlockInfoState>(Lifetime.Singleton);
            builder.Register<TrainHUDScreenState>(Lifetime.Singleton);
            builder.Register<BuildMenuState>(Lifetime.Singleton);
            builder.Register<BuildOperationHistory>(Lifetime.Singleton);
            builder.Register<BuildUndoService>(Lifetime.Singleton);
            builder.Register<ItemRecipeViewerDataContainer>(Lifetime.Singleton);
            builder.Register<GameScreenSubInventoryInteractService>(Lifetime.Singleton);
            builder.Register<PlacementTargetPickService>(Lifetime.Singleton);
            builder.Register<RideVehicleInputService>(Lifetime.Singleton);
            builder.Register<PauseMenuStateService>(Lifetime.Singleton);

            // プレイヤーステート（UIState → PlayerStateController の単方向依存）
            // Player state framework (one-way dependency: UIState → PlayerStateController)
            builder.Register<NormalPlayerState>(Lifetime.Singleton);
            builder.Register<TrainCarRideFollowTargetResolver>(Lifetime.Singleton).As<IRideFollowTargetResolver>();
            builder.Register<RidingPlayerState>(Lifetime.Singleton);
            builder.Register<PlayerStateDictionary>(Lifetime.Singleton);
            builder.Register<PlayerStateController>(Lifetime.Singleton).AsSelf().As<ITickable>();
            
            // スキット関連
            // register skit related
            var skitActionContext = new SkitActionContext();
            builder.RegisterInstance<ISkitActionContext>(skitActionContext);
            builder.RegisterInstance<ISkitActionController>(skitActionContext);
            // スキットJSONの位置はスポーン地点基準の相対座標（ADR 0029）
            // Skit JSON positions are relative to the spawn point (ADR 0029)
            builder.RegisterInstance(new SkitOrigin(initialHandshakeResponse.MapLayout.Spawn));
            
            // その他インスタンス
            // register other instance
            builder.Register<TutorialManager>(Lifetime.Singleton);
            builder.Register<IGameUnlockStateData, ClientGameUnlockStateData>(Lifetime.Singleton);
            builder.Register<RailGraphClientCache>(Lifetime.Singleton);
            builder.Register<ClientStationReferenceRegistry>(Lifetime.Singleton).AsSelf().As<IInitializable>().As<IDisposable>();
            builder.Register<RailGraphSnapshotApplier>(Lifetime.Singleton);
            builder.Register<TrainUnitClientCache>(Lifetime.Singleton);
            builder.Register<TrainUnitTickState>(Lifetime.Singleton);
            builder.Register<TrainUnitFutureMessageBuffer>(Lifetime.Singleton);
            builder.Register<TrainUnitSnapshotApplier>(Lifetime.Singleton);
            builder.Register<TrainUnitVisualUpdateSystem>(Lifetime.Singleton);
            builder.Register<TrainUnitClientSimulator>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.Register<TrainUnitHashVerifier>(Lifetime.Singleton).As<ITrainUnitHashTickGate>().As<IDisposable>();
            builder.Register<TrainUnitDebugOverlayPresenter>(Lifetime.Singleton).As<ITickable>().As<IDisposable>();
            
            
            //Hierarchy上にあるcomponent
            // register component on hierarchy
            builder.RegisterComponent(gameStateController);
            builder.RegisterComponent(blockGameObjectDataStore);
            builder.RegisterComponent(mapObjectGameObjectDatastore).AsSelf().As<IInitialEventApplyWaitTarget>().As<ISkitWorldObjectControl>();
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

            builder.RegisterComponent(mapObjectPin).AsSelf().As<ITutorialWorldPin>();
            builder.RegisterComponent(veinPin).AsSelf().As<ITutorialWorldPin>();
            builder.RegisterComponent(uiHighlightTutorialManager);
            builder.RegisterComponent(keyControlTutorialManager);
            builder.RegisterComponent(itemViewHighLightTutorialManager);
            builder.RegisterComponent(blockPlacePreviewTutorialManager);
            builder.RegisterComponent(uiDragGuideTutorialManager);
            
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
            
            //依存関係を解決
            // resolve dependency
            _resolver = builder.Build();
            _resolver.Resolve<BlockGameObjectDataStore>();
            _resolver.Resolve<OutcropGameObjectDatastore>();
            _resolver.Resolve<UIStateControl>();
            _resolver.Resolve<EntityObjectDatastore>();
            _resolver.Resolve<TrainCarObjectDatastore>();
            _resolver.Resolve<ChallengeManager>();
            _resolver.Resolve<PlayerSystemContainer>();
            _resolver.Resolve<SkitUI>();

            // 購読で成立するため生成しておく。割当変更に選択枠を追従させる
            // Instantiated because it only works through its subscription, keeping the selected slot in step with assignments
            _resolver.Resolve<HotbarSelectionReconciler>();

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
