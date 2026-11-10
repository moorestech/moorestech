using Client.Input;
using Client.Input.Pointer;
using Client.Skit.UI;
using Client.Starter.Registration.GameUi.Hud;
using Client.Starter.Registration.GameUi.Shared;
using Client.Starter.Registration.GameUi.Screens;
using Client.Ui.Foundation.Composite;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Parts;
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
    // Client.Ui の登録口。全体スイッチの分岐はここ 1 か所で、Presenter・View・部品はスイッチを読まない（plan §3.3 R1、設計書 §4.4・[A-16]）
    // Registration entry for Client.Ui; the global switch branches only here, and presenters, views and parts never read it (plan §3.3 R1, design §4.4, [A-16])
    internal static class MainGameUiRegistration
    {
        public static void Register(ContainerBuilder builder, GameUiRoot gameUiRoot, UiSystem uiSystem)
        {
            // Web の系では Client.Ui を 1 つも登録せず、GameUiRoot の層は空のまま残る。起動の系は UiSystemBootSelection がログに出している
            // In the Web system nothing from Client.Ui is registered and the GameUiRoot layers stay empty; UiSystemBootSelection has logged the boot's system
            if (uiSystem != UiSystem.GameUi) return;

            builder.RegisterComponent(gameUiRoot);
            // パネルの描き先を sRGB 合成の RT へ替えるので、root.panel を控える入口より先に初期化させる
            // It swaps the panel's target to the sRGB composite RT, so it initializes before entry points that keep root.panel
            builder.RegisterEntryPoint<GameUiSrgbCompositeInstaller>();
            builder.RegisterEntryPoint<GameUiLocaleClassBinder>();
            builder.RegisterEntryPoint<GameUiInputInstaller>();
            builder.RegisterEntryPoint<ImeCompositionModeApplier>();
            builder.Register<GameUiToastFeed>(Lifetime.Singleton);
            builder.Register<UiCommandResultToaster>(Lifetime.Singleton);
            builder.RegisterInstance(SkitPresentationStateStore.Instance);
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
            InventoryGameUiRegistration.Register(builder);
            ResearchGameUiRegistration.Register(builder);
        }
    }
}
