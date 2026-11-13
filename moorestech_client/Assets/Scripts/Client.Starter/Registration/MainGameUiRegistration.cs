using Client.Input.Pointer;
using Client.Starter.Registration.GameUi.Hud;
using Client.Starter.Registration.GameUi.Shared;
using Client.Starter.Registration.GameUi.Screens;
using Client.Starter.Registration.GameUi.Screens.Challenge;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Root;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Client.Ui.State.Recipe.Source;
using Client.Ui.State.Routing;
using Client.Ui.State.SkitPresence;
using Client.Ui.State.SlotCommands;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.SlotCommands.Player;
using Client.Ui.State.Toast;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration
{
    // Client.Ui の登録口。全体スイッチの分岐は呼び手の MainGameStarter（composition root）にあり、Presenter・View・部品はスイッチを読まない（plan §3.3 R1、設計書 §4.4・[A-16]）
    // Registration entry for Client.Ui; the global-switch branch lives in the caller MainGameStarter (composition root), and presenters, views and parts never read it (plan §3.3 R1, design §4.4, [A-16])
    internal static class MainGameUiRegistration
    {
        // GameUi の系でだけ呼ばれる。DI より前に立てた土台（sRGB 合成・倍率・ロケール）は作り済みのまま受け取る（S5-gate D-2）
        // Called only in the GameUi system; the foundation built before DI (sRGB composite, scale, locale) arrives as built (S5-gate D-2)
        public static void Register(ContainerBuilder builder, GameUiRoot gameUiRoot, GameUiPreDiFoundation gameUiFoundation)
        {
            builder.RegisterComponent(gameUiRoot);
            // 作り済みの合成 Tick と倍率を渡す
            // Hand over the built composite tick and scale
            gameUiFoundation.HandOverTo(builder);
            builder.RegisterEntryPoint<GameUiInputInstaller>();
            builder.RegisterEntryPoint<ImeCompositionModeApplier>();
            builder.Register<GameUiToastFeed>(Lifetime.Singleton);
            builder.Register<UiCommandResultToaster>(Lifetime.Singleton);
            builder.Register<BlockingSkitPresence>(Lifetime.Singleton);
            FoundationGameUiRegistration.Register(builder);
            // ポインタの位置の流れは画面ポインタ（InputSystemUiScreenPointer）が読む
            // The pointer position stream is read by the screen pointer (InputSystemUiScreenPointer)
            builder.Register<InputSystemPointerPositionStream>(Lifetime.Singleton);

            // ブロックインベントリの共有サービス: パネルの状態・遷移要求・スロット操作の型付きコマンド・ブロックの枠の並び・解放済み機械レシピ（S4 の各レーンが ctor で受ける）
            // Block-inventory shared services: panel state, transition requests, typed slot commands, block slot grids and unlocked machine recipes (taken by the S4 lanes' ctors)
            builder.RegisterEntryPoint<BlockInventoryPanelModel>().As<IBlockInventoryPanelSource>();
            builder.Register<UiStateControlTransitionRequester>(Lifetime.Singleton).As<IUiStateTransitionRequester>();
            builder.RegisterEntryPoint<UnlockedMachineRecipeSource>().AsSelf();
            builder.Register<SubInventoryRawSlots>(Lifetime.Singleton).As<IRawSubInventorySlots>();
            builder.Register<BlockSlotReader>(Lifetime.Singleton);
            builder.Register<LocalBlockInventoryOperations>(Lifetime.Singleton).As<IBlockInventoryOperations>();
            builder.Register<BlockItemActionExecutor>(Lifetime.Singleton);
            builder.Register<PlannedItemActionRunner>(Lifetime.Singleton);
            builder.Register<BlockSlotRestrictionInputBuilder>(Lifetime.Singleton);
            builder.Register<PlayerSlotContextReader>(Lifetime.Singleton);
            builder.Register<ShiftMoveRefusalNotifier>(Lifetime.Singleton);
            builder.Register<BlockSlotCommands>(Lifetime.Singleton).As<IBlockSlotCommands>();
            builder.Register<PlayerSlotCommands>(Lifetime.Singleton).As<IPlayerSlotCommands>();
            builder.Register<BlockItemGridFactory>(Lifetime.Singleton);
            // 追跡ブロックの液体
            // Fluids of the tracked block; taken by machine and train sections
            builder.Register<FluidNetworkReplicaReader>(Lifetime.Singleton).As<IFluidReplicaReader>();
            builder.RegisterEntryPoint<BlockFluidSlotsModel>().As<IBlockFluidSlotsModel>();

            // 画面登録区画: 1行ずつ呼び出しを足す
            // Screen registration section: add one call per line
            PauseMenuGameUiRegistration.Register(builder);
            ToastGameUiRegistration.Register(builder);
            HudGameUiRegistration.Register(builder);
            ContentGameUiRegistration.Register(builder);
            ModeHudGameUiRegistration.Register(builder);
            CursorTooltipGameUiRegistration.Register(builder);
            TutorialGameUiRegistration.Register(builder);
            GrabGameUiRegistration.Register(builder);
            BlockInventoryGameUiRegistration.Register(builder);
            BlockInventoryMachineGameUiRegistration.Register(builder);
            InventoryGameUiRegistration.Register(builder);
            BlockInventoryTrainGameUiRegistration.Register(builder);
            ResearchGameUiRegistration.Register(builder);
            BlockInventorySpecialGameUiRegistration.Register(builder);
            RecipeGameUiRegistration.Register(builder);
            BuildMenuGameUiRegistration.Register(builder);
            HotbarGameUiRegistration.Register(builder);
            ModalGameUiRegistration.Register(builder);
            NotificationGameUiRegistration.Register(builder);
            SkitGameUiRegistration.Register(builder);
            ChallengeGameUiRegistration.Register(builder);
        }
    }
}
