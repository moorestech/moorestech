using System.Collections.Generic;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     抽選の中身の応答を役割ごとの型で読む口。BlockNotFound はこの型に入らず、取得側（ポーラー）が扱う
    ///     Per-role read view of the draw-detail response; BlockNotFound is not part of it and is handled by the fetcher (poller)
    /// </summary>
    public abstract class CleanRoomYieldView
    {
        // 種別はこのファイルの型に閉じる
        // The kinds are closed to the types in this file
        private protected CleanRoomYieldView()
        {
        }
    }

    public sealed class CleanRoomYieldNoRoleView : CleanRoomYieldView
    {
        public static readonly CleanRoomYieldNoRoleView Instance = new();

        private CleanRoomYieldNoRoleView()
        {
        }
    }

    public sealed class CleanRoomYieldNotOperableView : CleanRoomYieldView
    {
        public static readonly CleanRoomYieldNotOperableView Instance = new();

        private CleanRoomYieldNotOperableView()
        {
        }
    }

    public sealed class CleanRoomYieldLayerExposureView : CleanRoomYieldView
    {
        public LayerExposureYieldMessagePack LayerExposure { get; }

        // 出力待ちの確定済み抽選に使った値。待っていなければ null（C7 で外す）
        // Values of the fixed draw waiting for output; null when nothing waits (removed in C7)
        public LayerExposureYieldMessagePack PendingDraw { get; }

        // 画面に出す最終の内訳と、失敗率の改善方法の行（並べる順。空ならボタンを出さない）
        // Final odds for the screen and the improvement rows in display order (empty means no button)
        public LayerExposureOddsMessagePack Odds { get; }
        public IReadOnlyList<CleanRoomYieldImprovementMessagePack> Improvements { get; }

        public CleanRoomYieldLayerExposureView(LayerExposureYieldMessagePack layerExposure, LayerExposureYieldMessagePack pendingDraw, LayerExposureOddsMessagePack odds, IReadOnlyList<CleanRoomYieldImprovementMessagePack> improvements)
        {
            LayerExposure = layerExposure;
            PendingDraw = pendingDraw;
            Odds = odds;
            Improvements = improvements;
        }
    }

    public sealed class CleanRoomYieldDicingView : CleanRoomYieldView
    {
        public DicingYieldMessagePack Dicing { get; }

        // 出力待ちの確定済み抽選に使った値。待っていなければ null（C7 で外す）
        // Values of the fixed draw waiting for output; null when nothing waits (removed in C7)
        public DicingYieldMessagePack PendingDraw { get; }

        // 画面に出す最終の内訳と、失敗率の改善方法の行（並べる順。空ならボタンを出さない）
        // Final odds for the screen and the improvement rows in display order (empty means no button)
        public DicingOddsMessagePack Odds { get; }
        public IReadOnlyList<CleanRoomYieldImprovementMessagePack> Improvements { get; }

        public CleanRoomYieldDicingView(DicingYieldMessagePack dicing, DicingYieldMessagePack pendingDraw, DicingOddsMessagePack odds, IReadOnlyList<CleanRoomYieldImprovementMessagePack> improvements)
        {
            Dicing = dicing;
            PendingDraw = pendingDraw;
            Odds = odds;
            Improvements = improvements;
        }
    }
}
