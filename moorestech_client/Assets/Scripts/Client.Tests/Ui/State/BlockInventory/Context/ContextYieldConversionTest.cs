using System.Collections.Generic;
using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using NUnit.Framework;
using Server.Protocol.PacketResponse;

namespace Client.Tests.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 確率の照会から欄への写し（最終の内訳と改善の行だけを読む）
    /// Mapping the odds query onto the entry (reads only final odds and improvement rows)
    /// </summary>
    public class ContextYieldConversionTest
    {
        private const BlockInfoFetchState Fetched = BlockInfoFetchState.Fetched;

        [Test]
        public void 未取得は取得状態で_役割なしと稼働できない機械は欄なし()
        {
            Assert.AreEqual(BlockInfoFetchState.TransportFailed, SectionContentAssert.Unavailable(ContextViewConversion.Yield(BlockInfoFetchState.TransportFailed, null, out _)));
            Assert.IsTrue(ContextViewConversion.Yield(Fetched, CleanRoomYieldNoRoleView.Instance, out _).IsAbsent);
            Assert.IsTrue(ContextViewConversion.Yield(Fetched, CleanRoomYieldNotOperableView.Instance, out _).IsAbsent);
        }

        [Test]
        public void 露光は失敗の割合と改善の行を写す()
        {
            var improvements = new List<CleanRoomYieldImprovementMessagePack> { CleanRoomYieldImprovementMessagePack.CreateRoomClassUp("B", new double[0], -0.15) };
            var odds = (LayerExposureYieldOdds)Converted(CleanRoomWireSamples.LayerExposure(0.35, improvements));

            Assert.AreEqual(0.35, odds.FailedRate);
            Assert.IsTrue(odds.HasImprovements);
            var classUp = (RoomClassUpOddsImprovement)odds.Improvements[0];
            Assert.AreEqual("B", classUp.TargetClassName);
            Assert.AreEqual(0, classUp.LevelRateDeltas.Count);
            Assert.AreEqual(-0.15, classUp.FailedRateDelta);
        }

        [Test]
        public void ダイシングは出力個数とLv別の最終割合と失敗と改善の行を並びのまま写す()
        {
            var improvements = new List<CleanRoomYieldImprovementMessagePack>
            {
                CleanRoomYieldImprovementMessagePack.CreateRoomClassUp("B", new[] { 0.092, 0.038 }, -0.131),
                CleanRoomYieldImprovementMessagePack.CreateAddQualityModule(1, new[] { 0.012, 0.015 }, -0.027),
            };
            var odds = (DicingYieldOdds)Converted(CleanRoomWireSamples.Dicing(300, new[] { 0.423, 0.072 }, 0.505, improvements));

            Assert.AreEqual(300, odds.OutputCount);
            CollectionAssert.AreEqual(new[] { 0.423, 0.072 }, odds.LevelRates);
            Assert.AreEqual(0.505, odds.FailedRate);
            Assert.AreEqual(2, odds.Improvements.Count);
            Assert.IsInstanceOf<RoomClassUpOddsImprovement>(odds.Improvements[0]);
            var module = (AddQualityModuleOddsImprovement)odds.Improvements[1];
            Assert.AreEqual(1, module.ModuleTier);
            CollectionAssert.AreEqual(new[] { 0.012, 0.015 }, module.LevelRateDeltas);
            Assert.AreEqual(-0.027, module.FailedRateDelta);
        }

        [Test]
        public void 改善の行が空なら空のまま写しボタンの条件は偽()
        {
            var odds = Converted(CleanRoomWireSamples.LayerExposure(0.1, new List<CleanRoomYieldImprovementMessagePack>()));
            Assert.AreEqual(0, odds.Improvements.Count);
            Assert.IsFalse(odds.HasImprovements);
        }

        [Test]
        public void 写せない種別の改善の行は欄を出さず異常を返す()
        {
            // 種別を足して写しを直し忘れたときの代わりに、写しに無い値を入れる
            // Stand in for a kind added without updating the mapping by setting a value the mapping lacks
            var unmapped = new CleanRoomYieldImprovementMessagePack((CleanRoomYieldImprovementKind)99, null, 1, new double[0], -0.1);
            var entry = ContextViewConversion.Yield(Fetched, CleanRoomWireSamples.LayerExposure(0.2, new List<CleanRoomYieldImprovementMessagePack> { unmapped }), out var anomaly);

            Assert.IsTrue(entry.IsAbsent);
            Assert.AreEqual("[ContextViewConversion] unmapped improvement kind: 99", anomaly, "ログは出さず異常の文面を返す");
        }

        private static CleanRoomYieldOdds Converted(CleanRoomYieldView view)
        {
            var odds = SectionContentAssert.Present(ContextViewConversion.Yield(Fetched, view, out var anomaly));
            Assert.IsNull(anomaly);
            return odds;
        }
    }
}
