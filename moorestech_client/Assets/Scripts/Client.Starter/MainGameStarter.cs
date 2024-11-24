using System.Diagnostics;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Control;
using Client.Game.InGame.Electric;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Mining;
using Client.Game.InGame.Player;
using Client.Game.InGame.Presenter.Command;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.Presenter.Player;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UI.Inventory.Sub;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.InGame.World;
using Client.Game.Sequence;
using Client.Game.Skit;
using Client.Game.Skit.Starter;
using Client.Network.API;
using Game.CraftChainer.Util;
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
        
        [SerializeField] private BlockGameObjectDataStore blockGameObjectDataStore;
        [SerializeField] private MapObjectGameObjectDatastore mapObjectGameObjectDatastore;
        
        [SerializeField] private CommandUIInput commandUIInput;
        [SerializeField] private HotBarView hotBarView;
        [SerializeField] private PlayerObjectController playerObjectController;
        [SerializeField] private MapObjectMiningController mapObjectMiningController;
        
        [SerializeField] private EntityObjectDatastore entityObjectDatastore;
        
        [SerializeField] private UIStateControl uIStateControl;
        [SerializeField] private PauseMenuObject pauseMenuObject;
        [SerializeField] private DeleteBarObject deleteBarObject;
        [SerializeField] private BlockInventoryView blockInventoryView;
        [SerializeField] private PlayerInventoryViewController playerInventoryViewController;
        [SerializeField] private CraftInventoryView craftInventoryView;
        [SerializeField] private MachineRecipeView machineRecipeView;
        [SerializeField] private RecipeViewerView recipeViewerView;
        [SerializeField] private ItemListView itemListView;
        
        [SerializeField] private MapObjectPin mapObjectPin;
        [SerializeField] private UIHighlightTutorialManager uiHighlightTutorialManager;
        [SerializeField] private KeyControlTutorialManager keyControlTutorialManager;
        
        [SerializeField] private BlockPlacePreview blockPlacePreview;
        [SerializeField] private SaveButton saveButton;
        [SerializeField] private BackToMainMenu backToMainMenu;
        [SerializeField] private NetworkDisconnectPresenter networkDisconnectPresenter;
        [SerializeField] private ChallengeManager challengeManager;
        
        [SerializeField] private PlayerSkitStarterDetector playerSkitStarterDetector;
        [SerializeField] private SkitManager skitManager;
        
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
            builder.RegisterEntryPoint<BlockStateEventHandler>();
            builder.RegisterEntryPoint<BlockPlaceSystem>().AsSelf();
            
            
            //UIコントロール
            // register UI control
            builder.Register<UIStateDictionary>(Lifetime.Singleton);
            builder.Register<BlockInventoryState>(Lifetime.Singleton);
            builder.Register<GameScreenState>(Lifetime.Singleton);
            builder.Register<PauseMenuState>(Lifetime.Singleton);
            builder.Register<PlayerInventoryState>(Lifetime.Singleton);
            builder.Register<DeleteBlockState>(Lifetime.Singleton);
            builder.Register<SkitState>(Lifetime.Singleton);
            builder.Register<PlaceBlockState>(Lifetime.Singleton);
            builder.Register<ItemRecipeViewerDataContainer>(Lifetime.Singleton);
            
            // チュートリアル関連
            // register tutorial
            builder.Register<TutorialManager>(Lifetime.Singleton);
            
            //Hierarchy上にあるcomponent
            // register component on hierarchy
            builder.RegisterComponent(blockGameObjectDataStore);
            builder.RegisterComponent(mapObjectGameObjectDatastore);
            
            builder.RegisterComponent(mainCamera);
            builder.RegisterComponent(commandUIInput);
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
            builder.RegisterComponent(blockInventoryView);
            builder.RegisterComponent(challengeManager);
            builder.RegisterComponent(craftInventoryView);
            builder.RegisterComponent(machineRecipeView);
            builder.RegisterComponent(recipeViewerView);
            builder.RegisterComponent(itemListView);
            
            builder.RegisterComponent(mapObjectPin);
            builder.RegisterComponent(uiHighlightTutorialManager);
            builder.RegisterComponent(keyControlTutorialManager);
            
            builder.RegisterComponent(playerSkitStarterDetector);
            builder.RegisterComponent(skitManager);
            
            builder.RegisterComponent(inGameCameraController);
            
            builder.RegisterComponent<IPlayerObjectController>(playerObjectController).AsSelf();
            builder.RegisterComponent<IBlockPlacePreview>(blockPlacePreview);
            
            builder.RegisterBuildCallback(objectResolver => { });
            
            //依存関係を解決
            // resolve dependency
            _resolver = builder.Build();
            _resolver.Resolve<BlockGameObjectDataStore>();
            _resolver.Resolve<CommandUIInput>();
            _resolver.Resolve<UIStateControl>();
            _resolver.Resolve<DisplayEnergizedRange>();
            _resolver.Resolve<EntityObjectDatastore>();
            _resolver.Resolve<ChallengeManager>();
            
            return _resolver;
        }
    }
}