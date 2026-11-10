using Client.Game.InGame.BlockInfoPolling;
using Client.Tests.Ui.Screens.BlockInventory.Machine;
using Client.Tests.Ui.State.BlockInventory.Context;
using Client.Tests.Ui.State.BlockInventory.Machine.Selection;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Panel;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 文脈の 3 欄の並びと出し入れ
    /// Order and visibility of the three context sections
    /// </summary>
    public class ContextSectionOrderTest : ContextSectionTestHost
    {
        private FakeBlockContextModel _context;
        private FakeMachineSectionModel _machine;

        private static readonly CleanRoomSummary Room = new(CleanRoomHeadline.Class("ISO 5"), 1, 1, new[] { new CleanRoomAirFilter(1, 1, 1) });

        [SetUp]
        public void SetUpModels()
        {
            _context = new FakeBlockContextModel();
            _machine = new FakeMachineSectionModel();
        }

        private IBlockInventoryPart MountBody()
        {
            var panel = BlockInventoryTestPanels.OfType("CleanRoomAirFilter");
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(panel));
            var body = BlockInventoryTestBodies.SectionStack(source, "CleanRoomAirFilter", new ContextSectionFactory(Content, _context, _machine));
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)panel);
            simulate.FrameUpdate();
            return body;
        }

        private static BlockContextViewModel RoomAndYield()
        {
            var room = ContextEntry<CleanRoomFetchedDetail>.Fetched(CleanRoomFetchedDetail.Enclosed(Room, CleanRoomHaltReason.None));
            var yield = ContextEntry<CleanRoomYieldFetched>.Fetched(CleanRoomYieldFetched.WithoutPendingDraw(CleanRoomYieldRates.LayerExposure(0.02)));
            return new BlockContextViewModel(room, yield, ContextEntry<MachineInputStall>.Absent);
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">部屋欄はセクション列の先頭に描く</web-test>
        [Test]
        public void 部屋欄はセクション列の先頭に描く()
        {
            _context.Set(RoomAndYield());
            var body = MountBody();

            CollectionAssert.AreEqual(new[] { "block-context-sections", "section-stack-item-grid" }, ShownNames(body.Root));
            Assert.Less(body.Root.Q<VisualElement>("clean-room-section").worldBound.y, body.Root.Q<VisualElement>("section-stack-item-grid").worldBound.y);
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">抽選の中身欄は部屋欄のすぐ下に描く</web-test>
        [Test]
        public void 抽選の中身欄は部屋欄のすぐ下に描く()
        {
            _context.Set(RoomAndYield());
            var body = MountBody();

            var sections = body.Root.Q<VisualElement>("block-context-sections");
            CollectionAssert.AreEqual(new[] { "clean-room-section-view", "clean-room-yield-section-view" }, ShownNames(sections[0]));
            Assert.Less(body.Root.Q<VisualElement>("clean-room-section").worldBound.y, body.Root.Q<VisualElement>("clean-room-yield-section").worldBound.y);
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">照会欄の無い payload では3欄とも描かず、取得中の欄は取得状態の行だけを描く</web-test>
        [Test]
        public void 照会欄の無いpayloadでは3欄とも描かず取得中の欄は取得状態の行だけを描く()
        {
            var body = MountBody();
            Assert.AreEqual(new[] { "section-stack-item-grid" }, ShownNames(body.Root));

            _context.Set(new BlockContextViewModel(ContextEntry<CleanRoomFetchedDetail>.Unavailable(BlockInfoFetchState.NotFetched), ContextEntry<CleanRoomYieldFetched>.Absent, ContextEntry<MachineInputStall>.Absent));
            simulate.FrameUpdate();

            Assert.AreEqual(new[] { "block-context-sections", "section-stack-item-grid" }, ShownNames(body.Root));
            Assert.IsNotNull(body.Root.Q<VisualElement>("clean-room-fetch-state"));
            Assert.IsNull(body.Root.Q<VisualElement>("clean-room-section"));
        }

        [Test]
        public void 取得済みから未取得へ戻ると部屋の値を捨てて取得状態の行へ替わり行数の変化にも追従する()
        {
            var body = MountBody();
            _context.Set(RoomAndYield());
            simulate.FrameUpdate();
            Assert.IsNotNull(body.Root.Q<VisualElement>("clean-room-air-filter-0"));

            _context.Set(new BlockContextViewModel(ContextEntry<CleanRoomFetchedDetail>.Unavailable(BlockInfoFetchState.Vanished), ContextEntry<CleanRoomYieldFetched>.Absent, ContextEntry<MachineInputStall>.Absent));
            simulate.FrameUpdate();

            Assert.IsNull(body.Root.Q<VisualElement>("clean-room-section"));
            Assert.IsNotNull(body.Root.Q<VisualElement>("clean-room-fetch-state"));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">搬入停止欄は機械欄より上に赤字で描く</web-test>
        [Test]
        public void 搬入停止欄は機械欄より上に赤字で描く()
        {
            // 機械では文脈を本文の器の先頭に置く
            // On machines the context leads the body box
            _machine.Set(SectionContent<MachineSectionViewModel>.Present(MachineScreenViewModels.WithState(MachineProcessDisplayState.Processing, 1, 1, false, false)));
            _context.Set(new BlockContextViewModel(ContextEntry<CleanRoomFetchedDetail>.Absent, ContextEntry<CleanRoomYieldFetched>.Absent, ContextEntry<MachineInputStall>.Fetched(new MachineInputStall(MachineInputStallReasonKind.MixedLevel, PlateId))));
            var panel = BlockInventoryTestPanels.OfType("ElectricMachine");
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(panel));
            var machineFactory = new MachineSectionFactory(_machine, _context, new RecordingBlockSlotCommands(), Toaster, Content, new RecordingRecipeSelectCommand(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier()));
            var body = BlockInventoryTestBodies.SectionStack(source, "ElectricMachine", new ContextSectionFactory(Content, _context, _machine), machineFactory);
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)panel);
            simulate.FrameUpdate();

            CollectionAssert.DoesNotContain(ShownNames(body.Root), "block-context-sections");
            var stall = body.Root.Q<Label>("machine-input-stall");
            Assert.IsTrue(stall.ClassListContains("lack-highlight-text--insufficient"));
            Assert.Less(stall.worldBound.yMin, body.Root.Q("machine-inventory-body").worldBound.yMin);
        }
    }
}
