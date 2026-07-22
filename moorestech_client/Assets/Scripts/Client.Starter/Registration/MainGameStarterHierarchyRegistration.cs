using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.BuildMenu;
using VContainer;
using VContainer.Unity;

namespace Client.Starter
{
    /// <summary>
    ///     MainGameStarterのHierarchy上コンポーネントのDI登録をまとめたヘルパ
    ///     Helper that groups hierarchy component DI registrations for MainGameStarter
    /// </summary>
    public static class MainGameStarterHierarchyRegistration
    {
        // Hierarchy上に配置されたコンポーネントを登録順を保って登録
        // Register hierarchy-placed components while preserving registration order
        public static void RegisterHierarchyComponents(IContainerBuilder builder, MainGameStarter starter)
        {
            builder.RegisterComponent(starter.gameStateController);
            builder.RegisterComponent(starter.blockGameObjectDataStore);
            builder.RegisterComponent(starter.mapObjectGameObjectDatastore);
            builder.RegisterComponent(starter.environmentRoot);

            builder.RegisterComponent(starter.mainCamera);
            builder.RegisterComponent(starter.hotBarView);

            builder.RegisterComponent(starter.uIStateControl);
            builder.RegisterComponent(starter.pauseMenuObject);
            builder.RegisterComponent(starter.deleteBarObject);
            builder.RegisterComponent(starter.buildMenuView).AsSelf().As<IBuildMenuView>();
            builder.RegisterComponent(starter.blueprintNameInputView);
            builder.RegisterComponent(starter.saveButton);
            builder.RegisterComponent(starter.backToMainMenu);
            builder.RegisterComponent(starter.networkDisconnectPresenter);
            builder.RegisterComponent(starter.mapObjectMiningController);

            builder.RegisterComponent(starter.displayEnergizedRange);
            builder.RegisterComponent(starter.entityObjectDatastore);
            builder.RegisterComponent(starter.trainCarObjectDatastore);
            builder.RegisterComponent(starter.playerInventoryViewController);
            builder.RegisterComponent(starter.challengeManager);
            builder.RegisterComponent(starter.craftInventoryView);
            builder.RegisterComponent(starter.machineRecipeView);
            builder.RegisterComponent(starter.recipeViewerView);
            builder.RegisterComponent(starter.itemListView);
            builder.RegisterComponent(starter.recipeTabView);
            builder.RegisterComponent(starter.challengeListView);
            builder.RegisterComponent(starter.researchTreeViewManager);

            builder.RegisterComponent<IMapObjectPin>(starter.mapObjectPin);
            builder.RegisterComponent(starter.uiHighlightTutorialManager);
            builder.RegisterComponent(starter.keyControlTutorialManager);
            builder.RegisterComponent(starter.itemViewHighLightTutorialManager);
            builder.RegisterComponent(starter.blockPlacePreviewTutorialManager);

            builder.RegisterComponent(starter.playerSystemContainer);
            builder.RegisterComponent(starter.skitManager).As<IInitializable>();
            builder.RegisterComponent(starter.skitUI);
            builder.RegisterComponent(starter.backgroundSkitManager);

            builder.RegisterComponent(starter.inGameCameraController).As<IInitializable>();

            builder.RegisterComponent<IPlacementPreviewBlockGameObjectController>(starter.previewBlockController);
            builder.RegisterComponent(starter.railConnectPreviewObject);
            builder.RegisterComponent(starter.trainRailObjectManager);
            builder.RegisterComponent(starter.trainCarObjectPreviewController);
        }
    }
}
