using UnityEngine;
using Response = Server.Protocol.PacketResponse.GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     抽選の中身の応答を役割ごとのビューへ読む唯一の口。必要な入れ子の欠けや壊れた改善の一覧は読めない扱いにする
    ///     The only reader turning a draw-detail response into per-role views; missing required nested odds and broken improvement lists are unreadable
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
                    view = CleanRoomYieldNoRoleView.Instance;
                    return BlockInfoReadResult.Success;
                case CleanRoomYieldResponseKind.NotOperable:
                    view = CleanRoomYieldNotOperableView.Instance;
                    return BlockInfoReadResult.Success;
                case CleanRoomYieldResponseKind.LayerExposure:
                    if (response.LayerExposureOdds == null) return WarnMissingNested();
                    if (!CleanRoomYieldImprovementMessagePack.AreReadable(response.Improvements, 0)) return WarnBrokenImprovements();
                    view = new CleanRoomYieldLayerExposureView(response.LayerExposureOdds, response.Improvements);
                    return BlockInfoReadResult.Success;
                case CleanRoomYieldResponseKind.Dicing:
                    if (response.DicingOdds == null || response.DicingOdds.LevelRates == null) return WarnMissingNested();
                    if (!CleanRoomYieldImprovementMessagePack.AreReadable(response.Improvements, response.DicingOdds.LevelRates.Length)) return WarnBrokenImprovements();
                    view = new CleanRoomYieldDicingView(response.DicingOdds, response.Improvements);
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

            // 外部から届いた wire の食い違いなので例外にせず、警告を出して読めない応答として扱う（ポーラーは値を捨てて再試行する）
            // A mismatch in wire data from outside, so warn instead of throwing and treat it as unreadable (the poller drops the value and retries)
            BlockInfoReadResult WarnMissingNested()
            {
                Debug.LogWarning($"[GetCleanRoomYieldInfo] {kind} response has no {kind} details; treating it as unreadable");
                return BlockInfoReadResult.Unreadable;
            }

            BlockInfoReadResult WarnBrokenImprovements()
            {
                Debug.LogWarning($"[GetCleanRoomYieldInfo] {kind} response has broken improvements; treating it as unreadable");
                return BlockInfoReadResult.Unreadable;
            }

            #endregion
        }
    }
}
