using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.State.BlockDetail.CleanRoom;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Server.Protocol.PacketResponse;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 部屋の照会を入力型へ写す
    /// Maps the room query onto L0-s4b's input type, as the Web host's CleanRoomDetailDtoBuilder
    /// 未知の型・値は異常の文面を返し、ログは呼び手の AnomalyReportGate が継続中 1 回だけ出す
    /// An unknown view or value returns the anomaly text, logged once while it persists by the caller's AnomalyReportGate
    /// </summary>
    public static class CleanRoomViewConversion
    {
        // 部屋欄を状態詳細から決める静的関数
        // Decides the room entry from target kind, fetch state and operability; static function split from the model
        public static SectionContent<CleanRoomFetchedDetail> Build(CleanRoomDetailTarget target, BlockInfoFetchState fetchState, CleanRoomInfoView view, CleanRoomOperabilityStateDetail operability, out string anomaly)
        {
            anomaly = null;
            if (target == CleanRoomDetailTarget.None) return SectionContent<CleanRoomFetchedDetail>.Absent;

            // 未取得は取得状態の 1 行
            // Not fetched yields the fetch-state line for any target, in the Web host's order
            if (fetchState != BlockInfoFetchState.Fetched) return Convert(fetchState, view, CleanRoomHaltReason.None, out anomaly);
            if (target == CleanRoomDetailTarget.AirFilter) return Convert(fetchState, view, CleanRoomHaltReason.None, out anomaly);

            // 室内機械の停止理由は状態詳細から
            // A fetched interior machine's halt reason comes from its state detail; no entry until present
            // silent-ok: 稼働可否の状態詳細が届く前は欄を出さない（Web ホストと同じ） / 届けば状態変化の購読で出る
            if (operability == null) return SectionContent<CleanRoomFetchedDetail>.Absent;
            if (!Enum.IsDefined(typeof(CleanRoomOperability), operability.Operability))
            {
                anomaly = $"Unreadable clean-room operability: {operability.Operability}";
                return SectionContent<CleanRoomFetchedDetail>.Absent;
            }
            if (!TryGetHaltReason(operability.Operability, out var haltReason, out anomaly)) return SectionContent<CleanRoomFetchedDetail>.Absent;
            return Convert(fetchState, view, haltReason, out anomaly);
        }

        private static SectionContent<CleanRoomFetchedDetail> Convert(BlockInfoFetchState fetchState, CleanRoomInfoView view, CleanRoomHaltReason haltReason, out string anomaly)
        {
            anomaly = null;
            if (fetchState != BlockInfoFetchState.Fetched) return SectionContent<CleanRoomFetchedDetail>.Unavailable(fetchState);
            switch (view)
            {
                case CleanRoomEnclosedView enclosed:
                    return SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Enclosed(Room(enclosed.Room), haltReason));
                case CleanRoomNotEnclosedView notEnclosed:
                    return Leak(notEnclosed.Leak, out anomaly);
            }

            // 未知の照会の型は欄を出さず開発者へ知らせる（購読の中で投げると購読ごと止まる。D-6）
            // An unknown query view hides the entry and tells developers (throwing inside the subscription would kill it; D-6)
            anomaly = $"[CleanRoomViewConversion] unsupported room query view: {view.GetType().Name}";
            return SectionContent<CleanRoomFetchedDetail>.Absent;

            #region Internal

            SectionContent<CleanRoomFetchedDetail> Leak(CleanRoomLeakView leak, out string leakAnomaly)
            {
                leakAnomaly = null;
                switch (leak)
                {
                    case CleanRoomOpeningsLeakView openings:
                        return SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Openings(openings.TotalOpeningCount, haltReason));
                    case CleanRoomNoEnclosureLeakView _:
                        return SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.NoEnclosure(haltReason));
                    case CleanRoomTooLargeLeakView tooLarge:
                        return SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.TooLarge(tooLarge.MaxRoomVolume, haltReason));
                    case CleanRoomDetectionPendingLeakView _:
                        return SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Detecting(haltReason));
                }
                leakAnomaly = $"[CleanRoomViewConversion] unsupported leak kind: {leak.GetType().Name}";
                return SectionContent<CleanRoomFetchedDetail>.Absent;
            }

            static CleanRoomSummary Room(CleanRoomSummaryMessagePack room)
            {
                var filters = new List<CleanRoomAirFilter>();
                foreach (var filter in room.AirFilters) filters.Add(new CleanRoomAirFilter(filter.PowerRatio, filter.FilterItemCount, filter.FilterRemainingRatio));
                var headline = room.TryGetClass(out var className, out _) ? CleanRoomHeadline.Class(className) : CleanRoomHeadline.ClassOut;
                return new CleanRoomSummary(headline, room.AirChangeRate, filters);
            }

            #endregion
        }

        // 列挙に無い稼働可否は偽（呼び手が欄を出さない）。新しい値の追加漏れは異常の文面で開発者へ知らせる（D-6）
        // An operability outside the enum returns false (the caller hides the entry); a missed new value comes back as the anomaly text (D-6)
        public static bool TryGetHaltReason(CleanRoomOperability operability, out CleanRoomHaltReason haltReason, out string anomaly)
        {
            anomaly = null;
            switch (operability)
            {
                case CleanRoomOperability.NotEnclosed:
                    haltReason = CleanRoomHaltReason.NotEnclosed;
                    return true;
                case CleanRoomOperability.ClassOut:
                    haltReason = CleanRoomHaltReason.ClassOut;
                    return true;
                case CleanRoomOperability.Operable:
                    haltReason = CleanRoomHaltReason.None;
                    return true;
            }
            anomaly = $"[CleanRoomViewConversion] unsupported operability: {operability}";
            haltReason = CleanRoomHaltReason.None;
            return false;
        }
    }
}
