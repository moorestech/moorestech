using System;
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
    /// 部屋の照会を照会元（室内機械／清浄機）ごとに入力型へ写す
    /// Maps the room query onto the input type per query source (interior machine or purifier)
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
            if (fetchState != BlockInfoFetchState.Fetched) return Convert(target, fetchState, view, CleanRoomHaltReason.None, out anomaly);
            if (target == CleanRoomDetailTarget.AirFilter) return Convert(target, fetchState, view, CleanRoomHaltReason.None, out anomaly);

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
            return Convert(target, fetchState, view, haltReason, out anomaly);
        }

        private static SectionContent<CleanRoomFetchedDetail> Convert(CleanRoomDetailTarget target, BlockInfoFetchState fetchState, CleanRoomInfoView view, CleanRoomHaltReason haltReason, out string anomaly)
        {
            anomaly = null;
            if (fetchState != BlockInfoFetchState.Fetched) return SectionContent<CleanRoomFetchedDetail>.Unavailable(fetchState);
            switch (view)
            {
                case CleanRoomEnclosedView enclosed:
                    return EnclosedRoom(enclosed.Room, out anomaly);
                case CleanRoomNotEnclosedView notEnclosed:
                    return Leak(notEnclosed.Leak, out anomaly);
            }

            // 未知の照会の型は欄を出さず開発者へ知らせる（購読の中で投げると購読ごと止まる。D-6）
            // An unknown query view hides the entry and tells developers (throwing inside the subscription would kill it; D-6)
            anomaly = $"[CleanRoomViewConversion] unsupported room query view: {view.GetType().Name}";
            return SectionContent<CleanRoomFetchedDetail>.Absent;

            #region Internal

            SectionContent<CleanRoomFetchedDetail> EnclosedRoom(CleanRoomSummaryMessagePack room, out string roomAnomaly)
            {
                roomAnomaly = null;
                var headline = room.TryGetClass(out var className, out _) ? CleanRoomHeadline.Class(className) : CleanRoomHeadline.ClassOut;

                // 室内機械は部屋クラスだけを読む（ADR 0105 §1）
                // An interior machine reads only the room class (ADR 0105 §1)
                if (target == CleanRoomDetailTarget.Machine) return SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForMachine(headline), haltReason));

                // 清浄機の照会は自分の状態を必ず持つ。無ければ欄を出さず開発者へ知らせる（D-6）
                // A purifier query always carries its own status; without it hide the entry and tell developers (D-6)
                if (!room.QueriedAirFilter.TryGetStatus(out var powerRatio, out var filterRemainingRatio))
                {
                    roomAnomaly = "[CleanRoomViewConversion] air-filter room query carries no queried air-filter status";
                    return SectionContent<CleanRoomFetchedDetail>.Absent;
                }
                var status = room.NextClass.TryGetUnitsToNextClass(out var nextClassName, out var units)
                    ? CleanRoomAirFilterRoomStatus.WithUnitsToNextClass(room.AirFilterCount, room.StoppedAirFilterCount, powerRatio, filterRemainingRatio, nextClassName, units)
                    : CleanRoomAirFilterRoomStatus.WithoutUnitsToNextClass(room.AirFilterCount, room.StoppedAirFilterCount, powerRatio, filterRemainingRatio);
                return SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForAirFilter(headline, status), haltReason));
            }

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
