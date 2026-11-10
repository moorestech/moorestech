using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.State.BlockDetail.CleanRoom;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 部屋の照会を入力型へ写す
    /// Maps the room query onto L0-s4b's input type, as the Web host's CleanRoomDetailDtoBuilder
    /// </summary>
    public static class CleanRoomViewConversion
    {
        // 部屋欄を状態詳細から決める静的関数
        // Decides the room entry from target kind, fetch state and operability; static function split from the model
        public static ContextEntry<CleanRoomFetchedDetail> Build(CleanRoomDetailTarget target, BlockInfoFetchState fetchState, CleanRoomInfoView view, CleanRoomOperabilityStateDetail operability)
        {
            if (target == CleanRoomDetailTarget.None) return ContextEntry<CleanRoomFetchedDetail>.Absent;

            // 未取得は取得状態の 1 行
            // Not fetched yields the fetch-state line for any target, in the Web host's order
            if (fetchState != BlockInfoFetchState.Fetched) return Convert(fetchState, view, CleanRoomHaltReason.None);
            if (target == CleanRoomDetailTarget.AirFilter) return Convert(fetchState, view, CleanRoomHaltReason.None);

            // 室内機械の停止理由は状態詳細から
            // A fetched interior machine's halt reason comes from its state detail; no entry until present
            // silent-ok: 稼働可否の状態詳細が届く前は欄を出さない（Web ホストと同じ） / 届けば状態変化の購読で出る
            if (operability == null) return ContextEntry<CleanRoomFetchedDetail>.Absent;
            if (!Enum.IsDefined(typeof(CleanRoomOperability), operability.Operability))
            {
                Debug.LogWarning($"Unreadable clean-room operability: {operability.Operability}");
                return ContextEntry<CleanRoomFetchedDetail>.Absent;
            }
            if (!TryGetHaltReason(operability.Operability, out var haltReason)) return ContextEntry<CleanRoomFetchedDetail>.Absent;
            return Convert(fetchState, view, haltReason);
        }

        private static ContextEntry<CleanRoomFetchedDetail> Convert(BlockInfoFetchState fetchState, CleanRoomInfoView view, CleanRoomHaltReason haltReason)
        {
            if (fetchState != BlockInfoFetchState.Fetched) return ContextEntry<CleanRoomFetchedDetail>.Unavailable(fetchState);
            switch (view)
            {
                case CleanRoomEnclosedView enclosed:
                    return ContextEntry<CleanRoomFetchedDetail>.Fetched(CleanRoomFetchedDetail.Enclosed(Room(enclosed.Room), haltReason));
                case CleanRoomNotEnclosedView notEnclosed:
                    return Leak(notEnclosed.Leak);
            }

            // 未知の照会の型は欄を出さず開発者へ知らせる（購読の中で投げると購読ごと止まる。D-6）
            // An unknown query view hides the entry and tells developers (throwing inside the subscription would kill it; D-6)
            Debug.LogError($"[CleanRoomViewConversion] unsupported room query view: {view.GetType().Name}");
            return ContextEntry<CleanRoomFetchedDetail>.Absent;

            #region Internal

            ContextEntry<CleanRoomFetchedDetail> Leak(CleanRoomLeakView leak)
            {
                switch (leak)
                {
                    case CleanRoomOpeningsLeakView openings:
                        return ContextEntry<CleanRoomFetchedDetail>.Fetched(CleanRoomFetchedDetail.Openings(openings.TotalOpeningCount, haltReason));
                    case CleanRoomNoEnclosureLeakView _:
                        return ContextEntry<CleanRoomFetchedDetail>.Fetched(CleanRoomFetchedDetail.NoEnclosure(haltReason));
                    case CleanRoomTooLargeLeakView tooLarge:
                        return ContextEntry<CleanRoomFetchedDetail>.Fetched(CleanRoomFetchedDetail.TooLarge(tooLarge.MaxRoomVolume, haltReason));
                    case CleanRoomDetectionPendingLeakView _:
                        return ContextEntry<CleanRoomFetchedDetail>.Fetched(CleanRoomFetchedDetail.Detecting(haltReason));
                }
                Debug.LogError($"[CleanRoomViewConversion] unsupported leak kind: {leak.GetType().Name}");
                return ContextEntry<CleanRoomFetchedDetail>.Absent;
            }

            static CleanRoomSummary Room(CleanRoomSummaryMessagePack room)
            {
                var filters = new List<CleanRoomAirFilter>();
                foreach (var filter in room.AirFilters) filters.Add(new CleanRoomAirFilter(filter.PowerRatio, filter.FilterItemCount, filter.FilterRemainingRatio));
                var headline = room.TryGetClass(out var className, out _) ? CleanRoomHeadline.Class(className) : CleanRoomHeadline.ClassOut;
                return new CleanRoomSummary(headline, room.Concentration, room.AirChangeRate, filters);
            }

            #endregion
        }

        // 列挙に無い稼働可否は偽（呼び手が欄を出さない）。新しい値の追加漏れを開発者へ知らせる（D-6）
        // An operability outside the enum returns false (the caller hides the entry); tells developers about a missed new value (D-6)
        public static bool TryGetHaltReason(CleanRoomOperability operability, out CleanRoomHaltReason haltReason)
        {
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
            Debug.LogError($"[CleanRoomViewConversion] unsupported operability: {operability}");
            haltReason = CleanRoomHaltReason.None;
            return false;
        }
    }
}
