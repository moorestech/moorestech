using Client.Game.InGame.BlockInfoPolling;
using Client.Tests.Ui.State.BlockInventory.Context;
using Client.Tests.Ui.State.BlockInventory.Machine.Selection;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.BlockInventory.Panel;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 標準の本文の中での文脈の 3 欄の並びと出し入れ（本物の工場・偽の Model で組む）
    /// Order and visibility of the three context sections inside the standard body (real factory, fake models)
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

        [Test]
        public void 部屋欄はセクション列の先頭に描く()
        {
            _context.Set(RoomAndYield());
            var body = MountBody();

            CollectionAssert.AreEqual(new[] { "block-context-sections", "section-stack-item-grid" }, ShownNames(body.Root));
            Assert.Less(body.Root.Q<VisualElement>("clean-room-section").worldBound.y, body.Root.Q<VisualElement>("section-stack-item-grid").worldBound.y);
        }

        [Test]
        public void 抽選の中身欄は部屋欄のすぐ下に描く()
        {
            _context.Set(RoomAndYield());
            var body = MountBody();

            var sections = body.Root.Q<VisualElement>("block-context-sections");
            CollectionAssert.AreEqual(new[] { "clean-room-section-view", "clean-room-yield-section-view" }, ShownNames(sections[0]));
            Assert.Less(body.Root.Q<VisualElement>("clean-room-section").worldBound.y, body.Root.Q<VisualElement>("clean-room-yield-section").worldBound.y);
        }

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
    }
}
