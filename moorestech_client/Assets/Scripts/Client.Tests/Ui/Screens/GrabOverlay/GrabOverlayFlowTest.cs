using System;
using System.Collections.Generic;
using System.Linq;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.Inventory;
using Client.Tests.Ui.State.Inventory.Drag;
using Client.Tests.Ui.State.ItemMove;
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.GrabOverlay;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;
using Client.Ui.State.Inventory.Grab;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using Client.Ui.State.Routing;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.GrabOverlay
{
    /// <summary>
    /// 押下の計画・進入・分割ドラッグを実行器とコマンドで通し、掴み絵が出て付いて来て個数が変わるまでを確かめる（L1）
    /// Runs press plans, enters and split drag through the executor and commands and checks the grab picture appears, follows and updates its count (L1)
    /// </summary>
    public class GrabOverlayFlowTest : ScreenTestHost
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private RecordingPlayerInventoryOperations _operations;
        private PlayerItemActionExecutor _executor;
        private FakePrimaryPointerButton _button;
        private PlayerSlotDragCommands _commands;
        private PlayerGrabModel _grab;
        private UiScreen _screen;
        private FakeUiScreenPointer _pointer;
        private GrabOverlayView _view;
        private Texture2D _texture;
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            // マスタはテスト用 mod、テクスチャは手元の 1 枚で Content を組む
            // Build Content with the test mod master and a local texture
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _operations = new RecordingPlayerInventoryOperations(36, 2);
            _operations.SetMain(0, new SlotItem(_wood, 10));
            _executor = new PlayerItemActionExecutor(_operations);
            _button = new FakePrimaryPointerButton(true);
            _commands = new PlayerSlotDragCommands(_operations, _executor, _button, Toaster);
            _commands.Initialize();
            _grab = new PlayerGrabModel(_operations);
            _grab.Initialize();
            _screen = UiScreen.PlayerInventory;
            _pointer = new FakeUiScreenPointer(1f);
            _view = new GrabOverlayView(_pointer, new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture)));
            UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Grab, UiFamily.Viewport).Add(_view);

            // 掴みの変化で同じ判断を描かせる（画面の変化の配線は GrabOverlayModel が持ち、録画で見る）
            // Render the same decision on grab changes (screen wiring lives in GrabOverlayModel and is checked by the recording)
            _grab.Current.Subscribe(_ => Render());
            simulate.FrameUpdate();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/inventory/inventory.spec.ts">左クリックで grab オーバーレイが追従する</web-test>
        [Test]
        public void 左押下で掴むと絵が出てポインタへ付いて来る()
        {
            Assert.AreEqual(DisplayStyle.None, _view.resolvedStyle.display, "掴む前");

            PressLeft(PlayerSlotRef.Main(0));
            _pointer.Move(new Vector2(500f, 300f));
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.Flex, _view.resolvedStyle.display);
            Assert.AreEqual(500f, _view.worldBound.center.x, 2f);
            Assert.AreEqual(300f, _view.worldBound.center.y, 2f);
        }

        /// <web-test file="e2e/tests/inventory/inventory.spec.ts">grab 中の右ドラッグで通過スロットへ1個ずつ配置する</web-test>
        [Test]
        public void 右ボタンを押したまま通ったスロットへ1個ずつ置く()
        {
            PressLeft(PlayerSlotRef.Main(0));

            Toaster.Report("inventory.move_item", _commands.EnterWithRightButton(PlayerSlotRef.Main(3)));
            Toaster.Report("inventory.move_item", _commands.EnterWithRightButton(PlayerSlotRef.Main(4)));
            simulate.FrameUpdate();

            Assert.AreEqual(new[] { "Grab->Main:3 x1", "Grab->Main:4 x1" }, _operations.Moves.Skip(_operations.Moves.Count - 2).ToArray());
            Assert.AreEqual("8", CountText(), "絵の個数");
            Assert.IsEmpty(ShownToasts);
        }

        /// <web-test file="e2e/tests/inventory/inventory.spec.ts">grab 中の左ドラッグは配分先だけを host へ送る</web-test>
        [Test]
        public void 左ボタンを押したまま通ったスロットへ等分し端数を掴んだまま残す()
        {
            PressLeft(PlayerSlotRef.Main(0));

            PressLeft(PlayerSlotRef.Main(3));
            _commands.EnterWithLeftButton(PlayerSlotRef.Main(4));
            _commands.EnterWithLeftButton(PlayerSlotRef.Main(5));
            _button.Release();
            _commands.LateTick();
            simulate.FrameUpdate();

            Assert.AreEqual(3, _operations.ReadMain(3).Count);
            Assert.AreEqual(3, _operations.ReadMain(4).Count);
            Assert.AreEqual(3, _operations.ReadMain(5).Count);
            Assert.AreEqual("1", CountText(), "絵の個数");
            Assert.IsEmpty(ShownToasts);
        }

        [Test]
        public void 掴みを扱わない画面へ移ると絵が隠れる()
        {
            PressLeft(PlayerSlotRef.Main(0));

            _screen = UiScreen.BuildMenu;
            Render();
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.None, _view.resolvedStyle.display);
        }

        private void Render()
        {
            _view.Render(GrabOverlayViewModelFactory.Create(_screen, _grab.Current.Value));
        }

        // 左押下は S4-alpha が配線する経路と同じく L0-s4a の計画を写して実行する
        // A left press converts and runs the L0-s4a plan, the same path S4-alpha wires
        private void PressLeft(PlayerSlotRef target)
        {
            var context = PlayerSlotContext.WithBlockClosed(_operations.ReadSnapshot(), new FixedMaxStackLookup(100));
            switch (PlayerSlotActionPlanner.PlanLeftDown(context, target.ToItemMoveSlotRef(), false))
            {
                case ItemActionsPlan planned:
                    Toaster.Report("inventory.move_item", _executor.Execute(Convert(planned)));
                    break;
                case BeginSplitDragPlan:
                    _commands.BeginSplitDrag(target);
                    break;
                default:
                    Assert.Fail($"想定外の計画 {target}");
                    break;
            }
            simulate.FrameUpdate();
        }

        private static List<PlayerItemAction> Convert(ItemActionsPlan planned)
        {
            var actions = new List<PlayerItemAction>();
            foreach (var action in planned.Actions)
            {
                Assert.IsTrue(PlayerItemActionConversion.TryConvert(action, out var playerAction), action.ToString());
                actions.Add(playerAction);
            }
            return actions;
        }

        private string CountText()
        {
            return _view.Query<TextElement>().ToList().Select(element => element.text).FirstOrDefault(text => !string.IsNullOrEmpty(text) && char.IsDigit(text[0]));
        }
    }
}
