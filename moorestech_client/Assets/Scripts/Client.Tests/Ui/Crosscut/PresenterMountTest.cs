using Client.Tests.Ui.Screens.GrabOverlay;
using System;
using System.Text.RegularExpressions;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens;
using Client.Tests.Ui.Screens.BlockInventory;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.Screens.Recipe;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Inventory;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Tests.Ui.State.Inventory.Panel;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.BlockInventory;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Recipe;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Routing;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Crosscut
{
    /// <summary>
    /// 持ち物・レシピ・ブロックの 3 Presenter を 1 本の画面ルータに結線し、画面ごとの出し入れを見る（L1。Web e2e uiState.spec の写し）
    /// Wires the inventory, recipe and block presenters to one screen router and checks per-screen visibility (L1; port of the Web e2e uiState.spec)
    /// </summary>
    public class PresenterMountTest : ScreenTestHost
    {
        private static readonly UIStateEnum[] SwitchableStates = { UIStateEnum.GameScreen, UIStateEnum.PlayerInventory, UIStateEnum.SubInventory, UIStateEnum.BuildMenu };

        private GameObject _host;
        private Texture2D _texture;
        private UIStateControl _control;
        private UiRoutingState _routing;
        private StageColumnsLayout _columns;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _host = new GameObject("presenter-mount");
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/uiState.spec.ts">既定(PlayerInventory)でインベントリ画面が表示される</web-test>
        [Test]
        public void 既定のインベントリ画面で持ち物とレシピが出る()
        {
            MountAll(UIStateEnum.PlayerInventory);

            Assert.IsTrue(IsShown(_columns.Q<InventoryView>()), "持ち物");
            Assert.IsTrue(IsShown(_columns.Q<RecipeViewerView>()), "レシピ");
        }

        /// <web-test file="e2e/tests/uiState.spec.ts">GameScreen でパネルが消え、PlayerInventory への event で再表示される</web-test>
        [Test]
        public void ゲーム画面で消えインベントリ画面へ戻ると再び出る()
        {
            MountAll(UIStateEnum.PlayerInventory);

            // ゲーム画面で両パネルが隠れ、インベントリ画面へ戻すと再び出る
            // Both panels hide on the game screen and reappear on returning to the inventory screen
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            Assert.IsFalse(IsShown(_columns.Q<InventoryView>()), "GameScreen の持ち物");
            Assert.IsFalse(IsShown(_columns.Q<RecipeViewerView>()), "GameScreen のレシピ");
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.PlayerInventory);
            Assert.IsTrue(IsShown(_columns.Q<InventoryView>()), "戻した持ち物");
            Assert.IsTrue(IsShown(_columns.Q<RecipeViewerView>()), "戻したレシピ");
        }

        /// <web-test file="e2e/tests/uiState.spec.ts">SubInventory でインベントリ+ブロックパネルが出てレシピビューアは消える</web-test>
        [Test]
        public void ブロックを開いた画面で持ち物とブロックが出てレシピは消える()
        {
            MountAll(UIStateEnum.SubInventory);

            Assert.IsTrue(IsShown(_columns.Q<InventoryView>()), "持ち物");
            Assert.IsTrue(IsShown(_columns.Q<BlockInventoryView>()), "ブロック");
            Assert.IsFalse(IsShown(_columns.Q<RecipeViewerView>()), "レシピ");
        }

        /// <web-test file="e2e/tests/uiState.spec.ts">許可表で拒否されたui_state.requestはtransition_not_allowedを返し画面を維持する</web-test>
        [Test]
        public void 許可表に無い遷移の要求は拒まれ画面がそのまま残る()
        {
            MountAll(UIStateEnum.BuildMenu);
            var requester = new UiStateControlTransitionRequester(_control);

            // ビルドメニューからインベントリ画面へは許可表に無いので拒み、拒否の理由をログへ出す
            // BuildMenu to PlayerInventory is not in the table, so it is refused with the reason logged
            LogAssert.Expect(LogType.Warning, new Regex(@"\[UiStateControlTransitionRequester\] transition refused: BuildMenu -> PlayerInventory is not allowed"));
            Assert.IsFalse(requester.RequestTransition(UIStateEnum.PlayerInventory));

            Assert.AreEqual(UIStateEnum.BuildMenu, _control.CurrentState);
            Assert.AreEqual(UiScreen.BuildMenu, _routing.Current.Value.Screen);
            Assert.IsFalse(IsShown(_columns.Q<InventoryView>()), "拒否後の持ち物");
        }

        private void MountAll(UIStateEnum initial)
        {
            // 本番と同じく 1 本の列と 1 本のルータへ 3 Presenter を結線する
            // Wire the three presenters to one column layout and one router, as in production
            _routing = UiRoutingStateTestFactory.CreateSwitchable(_host, initial, SwitchableStates, out _control);
            _columns = new StageColumnsLayout();
            rootVisualElement.Add(_columns);
            var content = new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture));
            var osCursor = new UiOsCursorRequestStack(new FakeUiOsCursorApplier(), new FakeUiScreenPointer(1f));
            var inventory = new RecordingPlayerInventoryOperations(36, 3);

            MountInventory();
            MountRecipe();
            MountBlockInventory();
            simulate.FrameUpdate();

            #region Internal

            void MountInventory()
            {
                // InventoryScreenVisibilityTest の SetUp と同じ組み立て
                // Same assembly as InventoryScreenVisibilityTest's SetUp
                var model = new PlayerInventoryPanelModel(inventory, new FakeEquipmentSelectionPort(3));
                model.Initialize();
                new InventoryScreenPresenter(_columns, _routing, model, new RecordingPlayerSlotCommands(), new RecordingInventorySortCommands(), content, Toaster, osCursor).Initialize();
            }

            void MountRecipe()
            {
                // Presenter は Model の初期値（未選択・空の一覧）だけを読む。Model の Initialize を回さないので解放・機械レシピ・イベントの源は読まれない
                // The presenter only reads the model's initial values; the model is never initialized, so the unlock, machine-recipe and event sources stay unread
                var selection = new RecipeItemSelection();
                var model = new RecipeScreenModel(null, null, null, null, inventory, selection, _routing);
                new RecipeScreenPresenter(_columns, _routing, model, content, new RecordingRecipeCommands(), Toaster, selection).Initialize();
            }

            void MountBlockInventory()
            {
                // BlockInventoryTestShell と同じ部品で、チェストを開いた源を本物の Presenter へ渡す
                // Same parts as BlockInventoryTestShell, feeding an opened-chest source to the real presenter
                var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Chest()));
                var layout = new BlockInventoryPanelLayoutModel(source, Array.Empty<IBlockInventoryLargePanelRule>());
                layout.Initialize();
                LogAssert.Expect(LogType.Log, new Regex("区画の登録が無い"));
                var standard = new SectionStackBodyFactory(BlockInventoryTestBodies.Grids(), new SectionStackSlotRegistry(Array.Empty<ISectionStackSectionFactory>()), source);
                var parts = new BlockInventoryPartRegistry(Array.Empty<IBlockInventoryBodyFactory>(), Array.Empty<IBlockInventoryTitleActionFactory>(), Array.Empty<ITrainInventoryBodyFactory>(), standard);
                new BlockInventoryScreenPresenter(_columns, _routing, layout, parts, new RecordingPanelCommands(), Toaster, osCursor).Initialize();
            }

            #endregion
        }
    }
}
