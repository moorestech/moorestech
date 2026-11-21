using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.State.BlockDetail.CleanRoom;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using NUnit.Framework;
using Server.Protocol.PacketResponse;

namespace Client.Tests.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 部屋と搬入停止の照会から欄への写し（部屋は照会元ごとに読む項目が違う。ADR 0105 §1・§5）
    /// Mapping the room and stall queries onto entries (the room reads different fields per query source; ADR 0105 §1, §5)
    /// </summary>
    public class ContextViewConversionTest
    {
        private const BlockInfoFetchState Fetched = BlockInfoFetchState.Fetched;
        private static readonly CleanRoomDetailTarget Machine = CleanRoomDetailTarget.Machine;
        private static readonly CleanRoomDetailTarget AirFilter = CleanRoomDetailTarget.AirFilter;

        // クラスC・清浄機6台（うち1台停止）・Bまであと5台・照会した清浄機は電力 100%・残量 50%
        // Class C, six purifiers (one stopped), five more to B, the queried purifier at full power and half life
        private static CleanRoomEnclosedView ClassC(CleanRoomNextClassMessagePack nextClass, CleanRoomQueriedAirFilterMessagePack queriedAirFilter)
        {
            return new CleanRoomEnclosedView(CleanRoomWireSamples.ClassifiedRoom("C", 2, 6, 1, nextClass, queriedAirFilter));
        }

        private static CleanRoomEnclosedView ClassC()
        {
            return ClassC(CleanRoomNextClassMessagePack.CreateUnitsToNextClass("B", 5), CleanRoomQueriedAirFilterMessagePack.CreateAirFilter(1, 0.5));
        }

        private static CleanRoomFetchedDetail Machined(CleanRoomInfoView view, CleanRoomOperability operability) =>
            SectionContentAssert.Present(CleanRoomViewConversion.Build(Machine, Fetched, view, new CleanRoomOperabilityStateDetail(operability), out _));

        private static CleanRoomAirFilterRoomStatus AirFiltered(CleanRoomInfoView view)
        {
            var detail = (EnclosedCleanRoomDetail)SectionContentAssert.Present(CleanRoomViewConversion.Build(AirFilter, Fetched, view, null, out var anomaly));
            Assert.IsNull(anomaly);
            Assert.IsInstanceOf<AirFilterCleanRoomSummary>(detail.Room, "清浄機の照会は清浄機から見た部屋を持つ");
            return ((AirFilterCleanRoomSummary)detail.Room).AirFilter;
        }

        [Test]
        public void 部屋の未取得は取得状態の1行で_室内機械は部屋クラスだけを写す()
        {
            var unavailable = CleanRoomViewConversion.Build(Machine, BlockInfoFetchState.NotFetched, null, null, out _);
            Assert.AreEqual(BlockInfoFetchState.NotFetched, SectionContentAssert.Unavailable(unavailable), "稼働可否の詳細が無くても取得状態を先に見る");

            var enclosed = (EnclosedCleanRoomDetail)Machined(ClassC(), CleanRoomOperability.Operable);
            Assert.AreEqual("C", ((ClassCleanRoomHeadline)enclosed.Room.Headline).ClassName);
            Assert.IsInstanceOf<MachineCleanRoomSummary>(enclosed.Room, "室内機械は清浄機の行を持たない");

            var outRoom = new CleanRoomEnclosedView(CleanRoomWireSamples.OutRoom(1, 0, CleanRoomNextClassMessagePack.CreateNotCounted(), CleanRoomQueriedAirFilterMessagePack.CreateNotAirFilter()));
            Assert.IsInstanceOf<ClassOutCleanRoomHeadline>(((EnclosedCleanRoomDetail)Machined(outRoom, CleanRoomOperability.ClassOut)).Room.Headline);
        }

        [Test]
        public void 清浄機は台数と止まった台数と次のクラスまでの台数と自分の電力と残量を写す()
        {
            var status = AirFiltered(ClassC());

            Assert.AreEqual(6, status.AirFilterCount);
            Assert.AreEqual(1, status.StoppedAirFilterCount);
            Assert.AreEqual(1.0, status.PowerRatio);
            Assert.AreEqual(0.5, status.FilterRemainingRatio);
            Assert.IsTrue(status.TryGetUnitsToNextClass(out var nextClassName, out var units));
            Assert.AreEqual("B", nextClassName);
            Assert.AreEqual(5, units);
        }

        [Test]
        public void 最上位_届かないときは次のクラスまでの台数を持たない()
        {
            var queried = CleanRoomQueriedAirFilterMessagePack.CreateAirFilter(1, 1);
            Assert.IsFalse(AirFiltered(ClassC(CleanRoomNextClassMessagePack.CreateAtBestClass(), queried)).TryGetUnitsToNextClass(out _, out _));
            Assert.IsFalse(AirFiltered(ClassC(CleanRoomNextClassMessagePack.CreateUnreachable("A"), queried)).TryGetUnitsToNextClass(out _, out _));
        }

        [Test]
        public void 清浄機の照会なのに自分の状態が無ければ欄を出さず異常を返す()
        {
            var view = ClassC(CleanRoomNextClassMessagePack.CreateNotCounted(), CleanRoomQueriedAirFilterMessagePack.CreateNotAirFilter());
            var entry = CleanRoomViewConversion.Build(AirFilter, Fetched, view, null, out var anomaly);

            Assert.IsTrue(entry.IsAbsent);
            Assert.AreEqual("[CleanRoomViewConversion] air-filter room query carries no queried air-filter status", anomaly, "ログは出さず異常の文面を返す");
        }

        [Test]
        public void 部屋の漏れは種類ごとの値を写す()
        {
            var openings = (OpeningsCleanRoomDetail)Machined(new CleanRoomNotEnclosedView(new CleanRoomOpeningsLeakView(7)), CleanRoomOperability.Operable);
            Assert.AreEqual(7, openings.OpeningCount);
            Assert.IsInstanceOf<NoEnclosureCleanRoomDetail>(Machined(new CleanRoomNotEnclosedView(CleanRoomNoEnclosureLeakView.Instance), CleanRoomOperability.Operable));
            Assert.AreEqual(900, ((TooLargeCleanRoomDetail)Machined(new CleanRoomNotEnclosedView(new CleanRoomTooLargeLeakView(900)), CleanRoomOperability.Operable)).MaxRoomVolume);
            Assert.IsInstanceOf<DetectingCleanRoomDetail>(Machined(new CleanRoomNotEnclosedView(CleanRoomDetectionPendingLeakView.Instance), CleanRoomOperability.Operable));
        }

        [Test]
        public void 停止理由は室内機械の稼働可否から_清浄機はNone()
        {
            var view = ClassC();
            Assert.AreEqual(CleanRoomHaltReason.NotEnclosed, Machined(view, CleanRoomOperability.NotEnclosed).HaltReason);
            Assert.AreEqual(CleanRoomHaltReason.ClassOut, Machined(view, CleanRoomOperability.ClassOut).HaltReason);
            Assert.AreEqual(CleanRoomHaltReason.None, Machined(view, CleanRoomOperability.Operable).HaltReason);
            var airFilter = CleanRoomViewConversion.Build(AirFilter, Fetched, view, null, out _);
            Assert.AreEqual(CleanRoomHaltReason.None, SectionContentAssert.Present(airFilter).HaltReason);

            Assert.IsFalse(CleanRoomViewConversion.TryGetHaltReason((CleanRoomOperability)99, out _, out var anomaly));
            Assert.AreEqual("[CleanRoomViewConversion] unsupported operability: 99", anomaly, "ログは出さず異常の文面を返す");
        }

        [Test]
        public void 室内機械で取得済みなのに稼働可否の詳細が無い_または列挙外なら欄なし()
        {
            var view = ClassC();
            Assert.IsTrue(CleanRoomViewConversion.Build(Machine, Fetched, view, null, out _).IsAbsent);
            Assert.IsTrue(CleanRoomViewConversion.Build(CleanRoomDetailTarget.None, Fetched, view, null, out _).IsAbsent);

            Assert.IsTrue(CleanRoomViewConversion.Build(Machine, Fetched, view, new CleanRoomOperabilityStateDetail((CleanRoomOperability)99), out var anomaly).IsAbsent);
            Assert.AreEqual("Unreadable clean-room operability: 99", anomaly, "ログは出さず異常の文面を返す");
        }

        [Test]
        public void 詰まりは未取得なら取得状態_停止なら理由と品_止まっていなければ欄なし()
        {
            Assert.AreEqual(BlockInfoFetchState.NotFetched, SectionContentAssert.Unavailable(ContextViewConversion.Stall(BlockInfoFetchState.NotFetched, null, out _)));
            var stalled = ContextViewConversion.Stall(Fetched, new MachineInputStalledView(new MachineInputStallMessagePack(MachineInputStallReasonKind.AboveCraftCap, 5)), out _);
            Assert.IsTrue(stalled.TryGetPresent(out var stall));
            Assert.AreEqual(MachineInputStallReasonKind.AboveCraftCap, stall.Reason);
            Assert.AreEqual(new ItemId(5), stall.WaitingItemId);
            Assert.IsTrue(ContextViewConversion.Stall(Fetched, MachineInputNotStalledView.Instance, out _).IsAbsent);
        }

        [Test]
        public void 区画を出すのは機械でなく_3欄のどれかが無しでないときだけ()
        {
            var stall = SectionContent<MachineInputStall>.Present(new MachineInputStall(MachineInputStallReasonKind.MixedLevel, new ItemId(1)));
            var withStall = new BlockContextViewModel(SectionContent<CleanRoomFetchedDetail>.Absent, SectionContent<CleanRoomYieldOdds>.Absent, stall);
            var unavailableRoom = new BlockContextViewModel(SectionContent<CleanRoomFetchedDetail>.Unavailable(BlockInfoFetchState.NotFetched), SectionContent<CleanRoomYieldOdds>.Absent, SectionContent<MachineInputStall>.Absent);

            Assert.IsFalse(ContextSectionVisibility.Shows(true, withStall), "機械は専用の区画");
            Assert.IsFalse(ContextSectionVisibility.Shows(false, BlockContextViewModel.None), "3 欄とも無し");
            Assert.IsTrue(ContextSectionVisibility.Shows(false, withStall));
            Assert.IsTrue(ContextSectionVisibility.Shows(false, unavailableRoom), "未取得も無しではない");
        }
    }
}
