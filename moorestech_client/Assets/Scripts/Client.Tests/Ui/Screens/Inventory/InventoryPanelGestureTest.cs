using System;
using System.Linq;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Panel;
using Client.Ui.Content;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class InventoryPanelGestureTest : ScreenTestHost
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid StoneGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        private InventoryScreenFixture _fixture;
        private InventoryView _view;
        private Texture2D _texture;
        private ItemId _wood;
        private ItemId _stone;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            _stone = MasterHolder.ItemMaster.GetItemId(StoneGuid);
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _fixture = new InventoryScreenFixture(36, 3, Toaster);
            var content = new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture));
            _view = Mount(new InventoryView(new PlayerSlotGestureRouter(_fixture.Commands, Toaster), new RecordingInventorySortCommands(), content, Toaster));
            _view.SetShown(true);
            _fixture.Model.Current.Subscribe(_view.Render);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/inventory/inventory.spec.ts">ダブルクリックで同種を集約し、collect はクリックされたスロットを送る</web-test>
        [Test]
        public void ダブルクリックは集める操作を1回だけ呼びトーストを出さない()
        {
            _fixture.Operations.SetMain(0, new SlotItem(_wood, 10));
            _fixture.Operations.SetMain(2, new SlotItem(_wood, 5));
            simulate.FrameUpdate();

            simulate.DoubleClick(Frame(0));
            _fixture.Button.Release();
            _fixture.Drag.LateTick();

            // 集める先（掴みか枠か）は計画が決める。Web と同じく押した枠を起点に 1 回だけ集め、良性の失敗はトーストにしない
            // The planner decides the target (grab or slot); as on the Web, collect runs once from the clicked slot and benign failures do not toast
            Assert.AreEqual(1, _fixture.Operations.Collects.Count, string.Join(",", _fixture.Operations.Collects));
            Assert.IsEmpty(ShownToasts);
        }

        /// <web-test file="e2e/tests/inventory/inventory.spec.ts">右クリックで inventory.split を送る</web-test>
        [Test]
        public void 空手の右押下は半分を掴む()
        {
            _fixture.Operations.SetMain(0, new SlotItem(_wood, 10));
            simulate.FrameUpdate();

            simulate.MouseDown(Frame(0).worldBound.center, MouseButton.RightMouse);
            simulate.MouseUp(Frame(0).worldBound.center, MouseButton.RightMouse);

            CollectionAssert.Contains(_fixture.Operations.Moves, "Main:0->Grab x5");
            Assert.AreEqual(5, _fixture.Operations.ReadGrab().Count);
        }

        /// <web-test file="e2e/tests/inventory/inventory.spec.ts">grab 中に別アイテムのスロットを押すと入れ替えの move を送りトーストを出さない</web-test>
        [Test]
        public void 掴み中に別アイテムの枠を押すと入れ替えてトーストを出さない()
        {
            _fixture.Operations.SetMain(0, new SlotItem(_wood, 10));
            _fixture.Operations.SetMain(1, new SlotItem(_stone, 2));
            simulate.FrameUpdate();

            PressAndRelease(0);
            PressAndRelease(1);

            Assert.AreEqual("Grab->Main:1 x10", _fixture.Operations.Moves.Last());
            Assert.AreEqual(_wood, _fixture.Operations.ReadMain(1).ItemId);
            Assert.AreEqual(_stone, _fixture.Operations.ReadGrab().ItemId);
            Assert.IsEmpty(ShownToasts);
        }

        private VisualElement Frame(int index)
        {
            return Find<VisualElement>(_view, $"main-slot-{index}");
        }

        private void PressAndRelease(int index)
        {
            // 左押下 → 解放 → LateTick で分割ドラッグが確定する（C4-e D-29）
            // Left press, release and LateTick commit the split drag (C4-e D-29)
            simulate.MouseDown(Frame(index).worldBound.center, MouseButton.LeftMouse);
            simulate.MouseUp(Frame(index).worldBound.center, MouseButton.LeftMouse);
            _fixture.Button.Release();
            _fixture.Drag.LateTick();
            _fixture.Button.SetPressed(true);
        }
    }
}
