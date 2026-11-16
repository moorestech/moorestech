using System;
using System.Linq;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Tests.Ui.Screens.Settings;
using Client.Tests.Ui.State.Inventory.Panel;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.PauseMenu;
using Core.Master;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Crosscut.TextSelection
{
    /// <summary>
    /// 持ち物・ビルドメニュー・ポーズメニューの文は選択できず、ビルドメニューの検索欄だけ選択できることを確かめる（L1。Web e2e textSelection.spec の写し）
    /// Verifies inventory, build-menu and pause-menu copy is unselectable while the build-menu search field stays selectable (L1; port of the Web e2e textSelection.spec)
    /// </summary>
    public class TextSelectionTest : BuildMenuScreenTestHost
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        private InventoryView _inventory;
        private FakePauseMenuModel _pauseModel;
        private PauseMenuView _pauseMenu;

        [SetUp]
        public void MountScreens()
        {
            // ビルドメニューは基底が組む。持ち物とポーズメニューを並べて載せる
            // The base assembles the build menu; mount the inventory and the pause menu beside it
            var cursor = new UiOsCursorRequestStack(new FakeUiOsCursorApplier());
            OpenMenu(BuildMenuSamples.Create());

            _inventory = Mount(new InventoryView(new PlayerSlotGestureRouter(new RecordingPlayerSlotCommands(), Toaster), new RecordingInventorySortCommands(), Content, Toaster, cursor));
            _inventory.SetShown(true);
            _inventory.Render(InventoryWithWood());

            _pauseModel = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.Top));
            var settings = new FakeSettingsModel(FakeSettingsModel.Default());
            _pauseMenu = Mount(new PauseMenuView(_pauseModel.Current, settings.Current, new RecordingPauseMenuCommands(), new RecordingSettingsCommands(), Toasts, Toaster, new BugReportDraft(), cursor));
            _pauseMenu.SetShown(true);
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/system/textSelection.spec.ts">入力欄以外はテキスト選択できない</web-test>
        [Test]
        public void 持ち物とビルドメニューとポーズメニューの文は選択できない()
        {
            AssertNoSelectableLabel(_inventory, "持ち物");
            AssertNoSelectableLabel(View, "ビルドメニュー");
            AssertNoSelectableLabel(_pauseMenu, "ポーズメニューのトップ");

            // 設定ページ描き直し後の文も選択不可
            // Copy redrawn for the settings page is unselectable too
            _pauseModel.Set(FakePauseMenuModel.Page(PauseMenuPage.Settings));
            simulate.FrameUpdate();
            AssertNoSelectableLabel(_pauseMenu, "ポーズメニューの設定");
        }

        /// <web-test file="e2e/tests/system/textSelection.spec.ts">建設メニューの検索入力はテキスト選択できる</web-test>
        [Test]
        public void ビルドメニューの検索欄は選択できる()
        {
            var search = Find<TextField>(View, "build-menu-search");

            Assert.IsTrue(IsShown(search), "検索欄が出ていない");
            Assert.IsTrue(search.textSelection.isSelectable, "検索欄の文が選択できない");
        }

        // 文が 1 つも無い空振りを避け、選択できる Label を名前つきで挙げる
        // Guard against a vacuous pass with no copy, and list selectable labels by name
        private static void AssertNoSelectableLabel(VisualElement screen, string screenName)
        {
            var labels = screen.Query<Label>().ToList();
            Assert.IsNotEmpty(labels, $"{screenName}に Label が無い");
            var selectable = labels.Where(label => label.selection.isSelectable).Select(label => $"'{label.name}' {label.text}").ToArray();
            Assert.IsEmpty(selectable, $"{screenName}に選択できる文がある: {string.Join(", ", selectable)}");
        }

        private static PlayerInventoryViewModel InventoryWithWood()
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var wood = new SlotItem(MasterHolder.ItemMaster.GetItemId(WoodGuid), 10);
            var main = Enumerable.Range(0, 36).Select(i => i == 0 ? wood : empty).ToArray();
            return new PlayerInventoryViewModel(main, new[] { empty, empty, empty }, 0, InventoryItemGuidTable.Build(main));
        }
    }
}
