using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Panel;
using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class InventoryViewTest : ScreenTestHost
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private RecordingPlayerSlotCommands _commands;
        private RecordingInventorySortCommands _sort;
        private InventoryView _view;
        private Texture2D _texture;
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _commands = new RecordingPlayerSlotCommands();
            _sort = new RecordingInventorySortCommands();
            var content = new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture));
            _view = Mount(new InventoryView(new PlayerSlotGestureRouter(_commands, Toaster), _sort, content, Toaster));
            _view.SetShown(true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/inventory/inventory.spec.ts">接続後にインベントリが描画される</web-test>
        [Test]
        public void 題と整理ボタンと持ち物の個数が描かれる()
        {
            _view.Render(Model(new SlotItem(_wood, 10)));
            simulate.FrameUpdate();

            Assert.IsTrue(_view.Query<Label>().ToList().Any(label => label.text == new UiText(LocalizationKeys.Ui.Inventory.Title).Resolve()), "題");
            var sort = Find<PanelActionButton>(_view, "inventory-sort");
            Assert.IsTrue(IsShown(sort), "整理ボタン");
            Assert.AreEqual(36, Find<VisualElement>(_view, "main-grid").Query(className: "slot-frame").ToList().Count, "36 枠");
            Assert.IsTrue(Find<VisualElement>(_view, "main-slot-0").Query<TextElement>().ToList().Any(text => text.text == "10"), "個数");

            // 整理ボタンは整理コマンドへ
            // The sort button goes to the sort command
            simulate.Click(sort);
            Assert.AreEqual(1, _sort.SortCount);
        }

        /// <web-test file="src/features/inventory/InventoryPanel/index.test.ts">先頭スロットのみ小文字guidでアンカーを名乗り、空/count0のスロットは無視される</web-test>
        [Test]
        public void 先頭スロットだけが小文字guidのアンカーを名乗る()
        {
            _view.Render(Model(new SlotItem(_wood, 3), new SlotItem(_wood, 2), new SlotItem(_wood, 0)));
            CollectionAssert.AreEqual(new[] { AnchorIds.InventoryItem(WoodGuid.ToString()) }, AnchorIdsInView());
            Assert.IsTrue(SlotOf(0).ClassListContains(AnchorIds.InventoryItem(WoodGuid.ToString())));
        }

        /// <web-test file="src/features/inventory/InventoryPanel/index.test.ts">アンカーは実所持の先頭スロット(4番目)に付き、そのスロットの中身と一致する</web-test>
        [Test]
        public void アンカーは実所持の先頭スロットに付き描き直しで移る()
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            _view.Render(Model(empty, new SlotItem(_wood, 0), empty, new SlotItem(_wood, 4)));
            Assert.IsTrue(SlotOf(3).ClassListContains(AnchorIds.InventoryItem(WoodGuid.ToString())), "4 番目");

            // 先頭が動いたら古い枠からは印ごと外れる
            // When the first owned slot moves, the old slot loses the id and the marker
            _view.Render(Model(new SlotItem(_wood, 1), empty, empty, new SlotItem(_wood, 4)));
            Assert.IsTrue(SlotOf(0).ClassListContains(AnchorIds.InventoryItem(WoodGuid.ToString())));
            Assert.IsFalse(SlotOf(3).ClassListContains(TutorialAnchor.MarkerClassName));
            Assert.AreEqual(1, AnchorIdsInView().Count);
        }

        [Test]
        public void 枠の身振りはプレイヤーの枠のコマンドへ渡る()
        {
            _view.Render(Model(new SlotItem(_wood, 10)));
            simulate.FrameUpdate();
            var frame = Find<VisualElement>(_view, "main-slot-2");

            simulate.MouseDown(frame.worldBound.center, MouseButton.RightMouse);
            simulate.MouseUp(frame.worldBound.center, MouseButton.RightMouse);

            CollectionAssert.Contains(_commands.Calls, "RightDown Main:2");
        }

        private PlayerInventoryViewModel Model(params SlotItem[] head)
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var main = Enumerable.Range(0, 36).Select(i => i < head.Length ? head[i] : empty).ToArray();
            return new PlayerInventoryViewModel(main, new[] { empty, empty, empty }, 0, InventoryItemGuidTable.Build(main));
        }

        private VisualElement SlotOf(int index)
        {
            // 印は ItemSlot に付く
            // Anchors sit on the ItemSlot
            return Find<VisualElement>(_view, $"main-slot-{index}").parent;
        }

        private List<string> AnchorIdsInView()
        {
            return _view.Query(className: TutorialAnchor.MarkerClassName).ToList()
                .SelectMany(element => element.GetClasses()).Where(name => name.StartsWith("inventory.item-")).ToList();
        }
    }
}
