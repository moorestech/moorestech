using System;
using Client.Game.InGame.BlockInfoPolling;
using Server.Protocol.PacketResponse;

namespace Client.WebUiHost.Game.Topics.BlockDetail.MachineInput
{
    /// <summary>
    ///     止まった理由のビューから、webui に送る欄を組み立てる
    ///     Builds the webui section from the stall view
    /// </summary>
    public static class MachineInputStallDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockNetworkInfoCache cache)
        {
            // 止まった理由を追うのは機械だけ
            // Only machines follow the stall
            if (!cache.IsTrackingMachineInputStall) return;
            var fetchState = cache.GetMachineInputStall(out var view);
            dto.MachineInputStall = Build(fetchState, view);
        }

        // 取得できていない間は取得状態だけの欄にする。取得済みで止まっていなければ欄なし
        // While not fetched, the section carries only the fetch state; once fetched and not stalled, no section
        public static MachineInputStallDetailDto Build(BlockInfoFetchState fetchState, MachineInputStallView latestView)
        {
            if (fetchState != BlockInfoFetchState.Fetched) return MachineInputStallDetailDto.CreateUnavailable(fetchState);
            return latestView switch
            {
                MachineInputStalledView stalled => MachineInputStallDetailDto.CreateStalled(ToWireReason(stalled.Stall.Reason), stalled.Stall.WaitingItemId),
                MachineInputNotStalledView _ => null,
                _ => throw new ArgumentOutOfRangeException(nameof(latestView), latestView.GetType().Name, "未対応の停止理由のビュー / Unsupported stall view"),
            };

            #region Internal

            // webui へ送る理由の文字列はこの網羅 switch の1か所でだけ作る
            // Reason strings for webui are made only in this exhaustive switch
            static string ToWireReason(MachineInputStallReasonKind reason)
            {
                return reason switch
                {
                    MachineInputStallReasonKind.MixedLevel => "mixedLevel",
                    MachineInputStallReasonKind.AboveCraftCap => "aboveCraftCap",
                    _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "未対応の停止理由 / Unsupported stall reason"),
                };
            }

            #endregion
        }
    }
}
