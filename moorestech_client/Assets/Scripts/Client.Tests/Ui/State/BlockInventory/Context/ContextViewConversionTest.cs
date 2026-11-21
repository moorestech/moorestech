using System.Collections.Generic;
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
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 照会から 3 欄への写し
    /// Mapping the room, draw and stall queries onto the three entries, as the Web host builders
    /// </summary>
    public class ContextViewConversionTest
    {
        private const BlockInfoFetchState Fetched = BlockInfoFetchState.Fetched;
        private static readonly CleanRoomDetailTarget Machine = CleanRoomDetailTarget.Machine;

        private static CleanRoomSummaryMessagePack Room(bool classified)
        {
            var filters = new List<CleanRoomAirFilterMessagePack> { new(new Vector3IntMessagePack(0, 0, 0), 0.5, 3, 2, 0.25) };
            var nextClass = CleanRoomNextClassMessagePack.CreateNotCounted();
            var queriedAirFilter = CleanRoomQueriedAirFilterMessagePack.CreateNotAirFilter();
            return classified ? CleanRoomSummaryMessagePack.CreateClassified("ISO5", 0, 2.5, 10, filters, 1, 0, nextClass, queriedAirFilter) : CleanRoomSummaryMessagePack.CreateOut(2.5, 10, filters, 1, 0, nextClass, queriedAirFilter);
        }

        private static CleanRoomFetchedDetail Machined(CleanRoomInfoView view, CleanRoomOperability operability) =>
            SectionContentAssert.Present(CleanRoomViewConversion.Build(Machine, Fetched, view, new CleanRoomOperabilityStateDetail(operability), out _));

        [Test]
        public void 部屋の未取得は取得状態の1行で_密閉は級と値を写す()
        {
            var unavailable = CleanRoomViewConversion.Build(Machine, BlockInfoFetchState.NotFetched, null, null, out _);
            Assert.AreEqual(BlockInfoFetchState.NotFetched, SectionContentAssert.Unavailable(unavailable), "稼働可否の詳細が無くても取得状態を先に見る");

            var enclosed = (EnclosedCleanRoomDetail)Machined(new CleanRoomEnclosedView(Room(true)), CleanRoomOperability.Operable);
            Assert.AreEqual("ISO5", ((ClassCleanRoomHeadline)enclosed.Room.Headline).ClassName);
            Assert.AreEqual(2.5, enclosed.Room.AirChangeRate);
            Assert.AreEqual(1, enclosed.Room.AirFilters.Count);
            Assert.AreEqual(2, enclosed.Room.AirFilters[0].FilterItemCount);

            var classOut = (EnclosedCleanRoomDetail)Machined(new CleanRoomEnclosedView(Room(false)), CleanRoomOperability.Operable);
            Assert.IsInstanceOf<ClassOutCleanRoomHeadline>(classOut.Room.Headline);
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
            var view = new CleanRoomEnclosedView(Room(true));
            Assert.AreEqual(CleanRoomHaltReason.NotEnclosed, Machined(view, CleanRoomOperability.NotEnclosed).HaltReason);
            Assert.AreEqual(CleanRoomHaltReason.ClassOut, Machined(view, CleanRoomOperability.ClassOut).HaltReason);
            Assert.AreEqual(CleanRoomHaltReason.None, Machined(view, CleanRoomOperability.Operable).HaltReason);
            var airFilter = CleanRoomViewConversion.Build(CleanRoomDetailTarget.AirFilter, Fetched, view, null, out _);
            Assert.AreEqual(CleanRoomHaltReason.None, SectionContentAssert.Present(airFilter).HaltReason);

            Assert.IsFalse(CleanRoomViewConversion.TryGetHaltReason((CleanRoomOperability)99, out _, out var anomaly));
            Assert.AreEqual("[CleanRoomViewConversion] unsupported operability: 99", anomaly, "ログは出さず異常の文面を返す");
        }

        [Test]
        public void 室内機械で取得済みなのに稼働可否の詳細が無い_または列挙外なら欄なし()
        {
            var view = new CleanRoomEnclosedView(Room(true));
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
