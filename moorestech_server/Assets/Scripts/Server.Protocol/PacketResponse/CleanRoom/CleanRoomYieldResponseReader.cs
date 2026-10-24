using UnityEngine;
using Response = Server.Protocol.PacketResponse.GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     抽選の中身の応答を役割ごとのビューへ読む唯一の口。判別子と違う役割の欄や、率の無い種別に付いた確定済み抽選は食い違いとして読めない扱いにする
    ///     The only reader turning a draw-detail response into per-role views; fields of another role, or a fixed draw on a kind without rates, are mismatches and unreadable
    /// </summary>
    public static class CleanRoomYieldResponseReader
    {
        public static BlockInfoReadResult Read(Response response, out CleanRoomYieldView view)
        {
            view = default;
            var kind = response.Kind;
            switch (kind)
            {
                case CleanRoomYieldResponseKind.NoRole:
                    if (HasPendingDraw()) return WarnMismatchedPending();
                    view = CleanRoomYieldNoRoleView.Instance;
                    return BlockInfoReadResult.Success;
                case CleanRoomYieldResponseKind.NotOperable:
                    if (HasPendingDraw()) return WarnMismatchedPending();
                    view = CleanRoomYieldNotOperableView.Instance;
                    return BlockInfoReadResult.Success;
                case CleanRoomYieldResponseKind.WaferGrade:
                    if (response.WaferGrade == null) return WarnMissingNested();
                    if (response.PendingLayerExposure != null || response.PendingDicing != null) return WarnMismatchedPending();
                    view = new CleanRoomYieldWaferGradeView(response.WaferGrade, response.PendingWaferGrade);
                    return BlockInfoReadResult.Success;
                case CleanRoomYieldResponseKind.LayerExposure:
                    if (response.LayerExposure == null) return WarnMissingNested();
                    if (response.PendingWaferGrade != null || response.PendingDicing != null) return WarnMismatchedPending();
                    view = new CleanRoomYieldLayerExposureView(response.LayerExposure, response.PendingLayerExposure);
                    return BlockInfoReadResult.Success;
                case CleanRoomYieldResponseKind.Dicing:
                    if (response.Dicing == null) return WarnMissingNested();
                    if (response.PendingWaferGrade != null || response.PendingLayerExposure != null) return WarnMismatchedPending();
                    view = new CleanRoomYieldDicingView(response.Dicing, response.PendingDicing);
                    return BlockInfoReadResult.Success;
                case CleanRoomYieldResponseKind.BlockNotFound:
                    return BlockInfoReadResult.BlockNotFound;
                case CleanRoomYieldResponseKind.UnsupportedTarget:
                    return BlockInfoReadResult.UnsupportedTarget;
                default:
                    Debug.LogWarning($"Unreadable response kind: {kind}");
                    return BlockInfoReadResult.Unreadable;
            }

            #region Internal

            bool HasPendingDraw()
            {
                return response.PendingWaferGrade != null || response.PendingLayerExposure != null || response.PendingDicing != null;
            }

            // 外部から届いた wire の食い違いなので例外にせず、警告を出して読めない応答として扱う（ポーラーは値を捨てて再試行する）
            // A mismatch in wire data from outside, so warn instead of throwing and treat it as unreadable (the poller drops the value and retries)
            BlockInfoReadResult WarnMissingNested()
            {
                Debug.LogWarning($"[GetCleanRoomYieldInfo] {kind} response has no {kind} details; treating it as unreadable");
                return BlockInfoReadResult.Unreadable;
            }

            BlockInfoReadResult WarnMismatchedPending()
            {
                Debug.LogWarning($"[GetCleanRoomYieldInfo] {kind} response carries a fixed draw of another role; treating it as unreadable");
                return BlockInfoReadResult.Unreadable;
            }

            #endregion
        }
    }
}
