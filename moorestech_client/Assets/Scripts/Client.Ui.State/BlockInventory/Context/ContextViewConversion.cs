using System.Collections.Generic;
using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Core.Master;
using Server.Protocol.PacketResponse;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 確率と搬入停止の照会を欄へ写す。未知のビュー・写せない改善の種別は異常の文面を返し、ログは呼び手の AnomalyReportGate が継続中 1 回だけ出す。壊れた改善の行は読み口が読めない応答にする
    /// Maps the odds and stall queries onto entries; unknown views or unmapped improvement kinds return the anomaly text, logged once while it persists by the caller's AnomalyReportGate; the reader turns broken improvement rows into an unreadable response
    /// </summary>
    public static class ContextViewConversion
    {
        // 確率は最終の内訳と改善の行だけを読む（出力待ちの組・損失率・品質 Tier は読まない。ADR 0105 §2・§3）
        // Reads only the final odds and improvement rows (no output-wait set, loss rate or quality tier; ADR 0105 §2, §3)
        public static SectionContent<CleanRoomYieldOdds> Yield(BlockInfoFetchState fetchState, CleanRoomYieldView view, out string anomaly)
        {
            anomaly = null;
            if (fetchState != BlockInfoFetchState.Fetched) return SectionContent<CleanRoomYieldOdds>.Unavailable(fetchState);
            switch (view)
            {
                case CleanRoomYieldLayerExposureView layer:
                    if (!TryConvertImprovements(layer.Improvements, out var layerImprovements, out anomaly)) return SectionContent<CleanRoomYieldOdds>.Absent;
                    return SectionContent<CleanRoomYieldOdds>.Present(CleanRoomYieldOdds.LayerExposure(layer.Odds.FailedRate, layerImprovements));
                case CleanRoomYieldDicingView dicing:
                    var odds = dicing.Odds;
                    if (!TryConvertImprovements(dicing.Improvements, out var dicingImprovements, out anomaly)) return SectionContent<CleanRoomYieldOdds>.Absent;
                    return SectionContent<CleanRoomYieldOdds>.Present(CleanRoomYieldOdds.Dicing(odds.OutputCount, odds.LevelRates, odds.FailedRate, dicingImprovements));
                // 役割の無いレシピ等は欄を出さない
                // Role-less recipes and machines that cannot operate show no entry; the room entry shows the halt reason
                case CleanRoomYieldNoRoleView _:
                case CleanRoomYieldNotOperableView _:
                    return SectionContent<CleanRoomYieldOdds>.Absent;
            }

            // 未知の型は欄を出さず開発者へ知らせる（購読の中で投げると購読ごと止まる。D-6）
            // An unknown view hides the entry and tells developers (throwing inside the subscription would kill it; D-6)
            anomaly = $"[ContextViewConversion] unsupported yield view: {view.GetType().Name}";
            return SectionContent<CleanRoomYieldOdds>.Absent;
        }

        // 改善の行をサーバーの並びのまま写す。差分の長さ・未定義の種別・クラス名は読み口（CleanRoomYieldImprovementMessagePack.AreReadable）が検査済みなので、ここでは検査しない（D-14）
        // Copies improvement rows in server order; delta length, undefined kinds and class names were checked by the reader (AreReadable), so nothing is re-checked here (D-14)
        private static bool TryConvertImprovements(IReadOnlyList<CleanRoomYieldImprovementMessagePack> packs, out List<CleanRoomOddsImprovement> improvements, out string anomaly)
        {
            improvements = new List<CleanRoomOddsImprovement>();
            anomaly = null;
            foreach (var pack in packs)
            {
                if (pack.TryGetRoomClassUp(out var targetClassName))
                {
                    improvements.Add(CleanRoomOddsImprovement.RoomClassUp(targetClassName, pack.LevelRateDeltas, pack.FailedRateDelta));
                    continue;
                }
                if (pack.TryGetAddQualityModule(out var moduleTier))
                {
                    improvements.Add(CleanRoomOddsImprovement.AddQualityModule(moduleTier, pack.LevelRateDeltas, pack.FailedRateDelta));
                    continue;
                }

                // 写せない種別は、種別を足してこの写しを直し忘れたとき（定義済みの種別は AreReadable を通る）。欄を出さず開発者へ知らせる（購読の中で投げると購読ごと止まる。D-6）
                // An unmapped kind means one was added without updating this mapping (defined kinds pass AreReadable); hide the entry and tell developers (throwing inside the subscription would kill it; D-6)
                anomaly = $"[ContextViewConversion] unmapped improvement kind: {pack.Kind}";
                return false;
            }
            return true;
        }

        public static SectionContent<MachineInputStall> Stall(BlockInfoFetchState fetchState, MachineInputStallView view, out string anomaly)
        {
            anomaly = null;
            if (fetchState != BlockInfoFetchState.Fetched) return SectionContent<MachineInputStall>.Unavailable(fetchState);
            switch (view)
            {
                case MachineInputStalledView stalled:
                    return SectionContent<MachineInputStall>.Present(new MachineInputStall(stalled.Stall.Reason, new ItemId(stalled.Stall.WaitingItemId)));
                case MachineInputNotStalledView _:
                    return SectionContent<MachineInputStall>.Absent;
            }

            // 未知の型は欄を出さず開発者へ知らせる（D-6）
            // An unknown view hides the entry and tells developers (D-6)
            anomaly = $"[ContextViewConversion] unsupported stall view: {view.GetType().Name}";
            return SectionContent<MachineInputStall>.Absent;
        }
    }
}
