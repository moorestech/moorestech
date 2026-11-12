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
using UnityEngine.TestTools;

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
            return classified ? CleanRoomSummaryMessagePack.CreateClassified("ISO5", 0, 1.5, 2.5, 10, filters) : CleanRoomSummaryMessagePack.CreateOut(1.5, 2.5, 10, filters);
        }

        private static T PresentValue<T>(SectionContent<T> entry)
        {
            Assert.IsTrue(entry.TryGetPresent(out var value), "取得済みの欄である");
            return value;
        }

        private static BlockInfoFetchState UnavailableState<T>(SectionContent<T> entry)
        {
            Assert.IsTrue(entry.TryGetUnavailable(out var fetchState), "取得できない欄である");
            return fetchState;
        }

        private static CleanRoomFetchedDetail Machined(CleanRoomInfoView view, CleanRoomOperability operability) =>
            PresentValue(CleanRoomViewConversion.Build(Machine, Fetched, view, new CleanRoomOperabilityStateDetail(operability)));

        [Test]
        public void 部屋の未取得は取得状態の1行で_密閉は級と値を写す()
        {
            var unavailable = CleanRoomViewConversion.Build(Machine, BlockInfoFetchState.NotFetched, null, null);
            Assert.AreEqual(BlockInfoFetchState.NotFetched, UnavailableState(unavailable), "稼働可否の詳細が無くても取得状態を先に見る");

            var enclosed = (EnclosedCleanRoomDetail)Machined(new CleanRoomEnclosedView(Room(true)), CleanRoomOperability.Operable);
            Assert.AreEqual("ISO5", ((ClassCleanRoomHeadline)enclosed.Room.Headline).ClassName);
            Assert.AreEqual(1.5, enclosed.Room.Concentration);
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
            var airFilter = CleanRoomViewConversion.Build(CleanRoomDetailTarget.AirFilter, Fetched, view, null);
            Assert.AreEqual(CleanRoomHaltReason.None, PresentValue(airFilter).HaltReason);

            LogAssert.Expect(LogType.Error, "[CleanRoomViewConversion] unsupported operability: 99");
            Assert.IsFalse(CleanRoomViewConversion.TryGetHaltReason((CleanRoomOperability)99, out _));
        }

        [Test]
        public void 室内機械で取得済みなのに稼働可否の詳細が無い_または列挙外なら欄なし()
        {
            var view = new CleanRoomEnclosedView(Room(true));
            Assert.IsTrue(CleanRoomViewConversion.Build(Machine, Fetched, view, null).IsAbsent);
            Assert.IsTrue(CleanRoomViewConversion.Build(CleanRoomDetailTarget.None, Fetched, view, null).IsAbsent);

            LogAssert.Expect(LogType.Warning, "Unreadable clean-room operability: 99");
            Assert.IsTrue(CleanRoomViewConversion.Build(Machine, Fetched, view, new CleanRoomOperabilityStateDetail((CleanRoomOperability)99)).IsAbsent);
        }

        [Test]
        public void 抽選は室内機械の層とダイシング_出力待ちの有無を分ける()
        {
            Assert.AreEqual(BlockInfoFetchState.TransportFailed, UnavailableState(ContextViewConversion.Yield(BlockInfoFetchState.TransportFailed, null)));

            var layer = Yielded(new CleanRoomYieldLayerExposureView(new LayerExposureYieldMessagePack(0.1), null));
            Assert.IsInstanceOf<SettledCleanRoomYield>(layer);
            Assert.AreEqual(0.1, ((LayerExposureYieldRates)layer.Current).LayerFailRate);
            var pendingLayer = (PendingDrawCleanRoomYield)Yielded(new CleanRoomYieldLayerExposureView(new LayerExposureYieldMessagePack(0.1), new LayerExposureYieldMessagePack(0.3)));
            Assert.AreEqual(0.3, ((LayerExposureYieldRates)pendingLayer.PendingDraw).LayerFailRate);

            var dicing = new DicingYieldMessagePack(0.2, 2, new[] { 0.5, 0.3 }, 0.1);
            var settled = Yielded(new CleanRoomYieldDicingView(dicing, null));
            Assert.AreEqual(2, ((DicingYieldRates)settled.Current).QualityTier);
            Assert.AreEqual(2, ((DicingYieldRates)settled.Current).LevelRates.Count);
            var pendingDicing = (PendingDrawCleanRoomYield)Yielded(new CleanRoomYieldDicingView(dicing, new DicingYieldMessagePack(0.4, 1, new[] { 0.9 }, 0.0)));
            Assert.AreEqual(0.4, ((DicingYieldRates)pendingDicing.PendingDraw).DieLossRate);

            Assert.IsTrue(ContextViewConversion.Yield(Fetched, CleanRoomYieldNoRoleView.Instance).IsAbsent);
            Assert.IsTrue(ContextViewConversion.Yield(Fetched, CleanRoomYieldNotOperableView.Instance).IsAbsent);

            #region Internal

            CleanRoomYieldFetched Yielded(CleanRoomYieldView view) => PresentValue(ContextViewConversion.Yield(Fetched, view));

            #endregion
        }

        [Test]
        public void 詰まりは未取得なら取得状態_停止なら理由と品_止まっていなければ欄なし()
        {
            Assert.AreEqual(BlockInfoFetchState.NotFetched, UnavailableState(ContextViewConversion.Stall(BlockInfoFetchState.NotFetched, null)));
            var stalled = ContextViewConversion.Stall(Fetched, new MachineInputStalledView(new MachineInputStallMessagePack(MachineInputStallReasonKind.AboveCraftCap, 5)));
            Assert.IsTrue(stalled.TryGetPresent(out var stall));
            Assert.AreEqual(MachineInputStallReasonKind.AboveCraftCap, stall.Reason);
            Assert.AreEqual(new ItemId(5), stall.WaitingItemId);
            Assert.IsTrue(ContextViewConversion.Stall(Fetched, MachineInputNotStalledView.Instance).IsAbsent);
        }

        [Test]
        public void 区画を出すのは機械でなく_3欄のどれかが無しでないときだけ()
        {
            var stall = SectionContent<MachineInputStall>.Present(new MachineInputStall(MachineInputStallReasonKind.MixedLevel, new ItemId(1)));
            var withStall = new BlockContextViewModel(SectionContent<CleanRoomFetchedDetail>.Absent, SectionContent<CleanRoomYieldFetched>.Absent, stall);
            var unavailableRoom = new BlockContextViewModel(SectionContent<CleanRoomFetchedDetail>.Unavailable(BlockInfoFetchState.NotFetched), SectionContent<CleanRoomYieldFetched>.Absent, SectionContent<MachineInputStall>.Absent);

            Assert.IsFalse(ContextSectionVisibility.Shows(true, withStall), "機械は専用の区画");
            Assert.IsFalse(ContextSectionVisibility.Shows(false, BlockContextViewModel.None), "3 欄とも無し");
            Assert.IsTrue(ContextSectionVisibility.Shows(false, withStall));
            Assert.IsTrue(ContextSectionVisibility.Shows(false, unavailableRoom), "未取得も無しではない");
        }
    }
}
