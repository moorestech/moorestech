using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Core.Master;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 抽選と搬入停止の照会を欄へ写す
    /// Maps the draw-detail and stall queries onto entries, as the Web host's builders
    /// </summary>
    public static class ContextViewConversion
    {
        public static ContextEntry<CleanRoomYieldFetched> Yield(BlockInfoFetchState fetchState, CleanRoomYieldView view)
        {
            if (fetchState != BlockInfoFetchState.Fetched) return ContextEntry<CleanRoomYieldFetched>.Unavailable(fetchState);
            switch (view)
            {
                case CleanRoomYieldLayerExposureView layer:
                    var layerCurrent = CleanRoomYieldRates.LayerExposure(layer.LayerExposure.LayerFailRate);
                    if (layer.PendingDraw == null) return Fetched(CleanRoomYieldFetched.WithoutPendingDraw(layerCurrent));
                    return Fetched(CleanRoomYieldFetched.WithPendingDraw(layerCurrent, CleanRoomYieldRates.LayerExposure(layer.PendingDraw.LayerFailRate)));
                case CleanRoomYieldDicingView dicing:
                    var dicingCurrent = Dicing(dicing.Dicing);
                    if (dicing.PendingDraw == null) return Fetched(CleanRoomYieldFetched.WithoutPendingDraw(dicingCurrent));
                    return Fetched(CleanRoomYieldFetched.WithPendingDraw(dicingCurrent, Dicing(dicing.PendingDraw)));
                // 役割の無いレシピ等は欄を出さない
                // Role-less recipes and machines that cannot operate show no entry; the room entry shows the halt reason
                case CleanRoomYieldNoRoleView _:
                case CleanRoomYieldNotOperableView _:
                    return ContextEntry<CleanRoomYieldFetched>.Absent;
            }

            // 未知の型は欄を出さず開発者へ知らせる（購読の中で投げると購読ごと止まる。D-6）
            // An unknown view hides the entry and tells developers (throwing inside the subscription would kill it; D-6)
            Debug.LogError($"[ContextViewConversion] unsupported yield view: {view.GetType().Name}");
            return ContextEntry<CleanRoomYieldFetched>.Absent;

            #region Internal

            ContextEntry<CleanRoomYieldFetched> Fetched(CleanRoomYieldFetched value) => ContextEntry<CleanRoomYieldFetched>.Fetched(value);

            CleanRoomYieldRates Dicing(DicingYieldMessagePack pack) => CleanRoomYieldRates.Dicing(pack.DieLossRate, pack.QualityTier, pack.LevelRates, pack.FailedRate);

            #endregion
        }

        public static ContextEntry<MachineInputStall> Stall(BlockInfoFetchState fetchState, MachineInputStallView view)
        {
            if (fetchState != BlockInfoFetchState.Fetched) return ContextEntry<MachineInputStall>.Unavailable(fetchState);
            switch (view)
            {
                case MachineInputStalledView stalled:
                    return ContextEntry<MachineInputStall>.Fetched(new MachineInputStall(stalled.Stall.Reason, new ItemId(stalled.Stall.WaitingItemId)));
                case MachineInputNotStalledView _:
                    return ContextEntry<MachineInputStall>.Absent;
            }

            // 未知の型は欄を出さず開発者へ知らせる（D-6）
            // An unknown view hides the entry and tells developers (D-6)
            Debug.LogError($"[ContextViewConversion] unsupported stall view: {view.GetType().Name}");
            return ContextEntry<MachineInputStall>.Absent;
        }
    }
}
