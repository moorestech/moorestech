using System;
using Core.Master;
using UnityEngine;

namespace Game.Block.Blocks.SpaceElevator
{
    // 入口の中身
    // - MissingPayload は空か1便未満
    // - 運べない品はマスタ変更後のロードでのみ残る
    // - 選んでいない種類は返しきれなかった選び直しで残る
    // What the inlet holds
    // - MissingPayload means empty or under one launch
    // - A non-carried item remains only after a master change on load
    // - A non-selected kind remains after an incomplete reselection refund
    internal enum SatelliteInletState
    {
        MissingPayload,
        Carriable,
        NotCarried,
        NotSelectedKind,
    }
    internal enum SatelliteTakeResult
    {
        Taken,
        MissingPayload,
        NotCarried,
        NotSelectedKind,
        OutputBlocked,
    }

    // 入口の中身を昇らない理由へ写し、塞がりの理由を開発者ログへ出す
    // Maps inlet contents to wait results and logs why the inlet is blocked
    internal static class SatelliteInletWait
    {
        public static bool BlocksInlet(SatelliteInletState state) => state is SatelliteInletState.NotCarried or SatelliteInletState.NotSelectedKind;
        public static bool BlocksInlet(SatelliteTakeResult result) => result is SatelliteTakeResult.NotCarried or SatelliteTakeResult.NotSelectedKind;

        public static SatelliteTakeResult ToWaitResult(SatelliteInletState state)
        {
            return state switch
            {
                SatelliteInletState.MissingPayload => SatelliteTakeResult.MissingPayload,
                // 未接続の拠点では運べる積荷も昇らないため、積荷待ちと同じ扱いにする
                // On an unconnected base even a carriable payload does not climb, so treat it as waiting
                SatelliteInletState.Carriable => SatelliteTakeResult.MissingPayload,
                SatelliteInletState.NotCarried => SatelliteTakeResult.NotCarried,
                SatelliteInletState.NotSelectedKind => SatelliteTakeResult.NotSelectedKind,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "昇らない理由へ写していない入口の状態です"),
            };
        }

        public static void LogBlocked(SatelliteTakeResult result, ItemId satelliteId)
        {
            if (result == SatelliteTakeResult.NotCarried) Debug.LogWarning($"[SpaceElevatorBase] 入口の {satelliteId} は今のマスタで運ぶ積荷ではないため昇れません。入口から取り出してください");
            else Debug.LogWarning($"[SpaceElevatorBase] 入口の {satelliteId} は選んだ積荷ではないため昇れません。入口から取り出してください");
        }
    }
}
