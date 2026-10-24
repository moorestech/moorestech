using System;
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
            // silent-ok: 未取得・消失・読めない応答の間は欄を出さない / 消失・読めない応答はポーラーが警告を1回出して問い合わせを止め（開き直すと再開）、未取得は次の1秒で欄が出る
            if (!cache.TryGetMachineInputStall(out var view)) return;
            dto.MachineInputStall = Build(view);
        }

        // webui へ送る理由の文字列はこの網羅 switch の1か所でだけ作る。止まっていなければ null（欄を出さない）
        // Reason strings for webui are made only in this exhaustive switch; null when not stalled (no section)
        public static MachineInputStallDetailDto Build(MachineInputStallView view)
        {
            return view switch
            {
                MachineInputStalledView stalled => new MachineInputStallDetailDto { Reason = ToWireReason(stalled.Stall.Reason), ItemId = stalled.Stall.WaitingItemId },
                MachineInputNotStalledView _ => null,
                _ => throw new ArgumentOutOfRangeException(nameof(view), view.GetType().Name, "未対応の停止理由のビュー / Unsupported stall view"),
            };

            #region Internal

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
