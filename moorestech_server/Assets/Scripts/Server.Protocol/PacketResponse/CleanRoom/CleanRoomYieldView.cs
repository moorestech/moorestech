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
        // 画面に出す最終の内訳と、失敗率の改善方法の行（並べる順。空ならボタンを出さない）
        // Final odds for the screen and the improvement rows in display order (empty means no button)
        public LayerExposureOddsMessagePack Odds { get; }
        public IReadOnlyList<CleanRoomYieldImprovementMessagePack> Improvements { get; }

        public CleanRoomYieldLayerExposureView(LayerExposureOddsMessagePack odds, IReadOnlyList<CleanRoomYieldImprovementMessagePack> improvements)
        {
            Odds = odds;
            Improvements = improvements;
        }
    }

    public sealed class CleanRoomYieldDicingView : CleanRoomYieldView
    {
        // 画面に出す最終の内訳と、失敗率の改善方法の行（並べる順。空ならボタンを出さない）
        // Final odds for the screen and the improvement rows in display order (empty means no button)
        public DicingOddsMessagePack Odds { get; }
        public IReadOnlyList<CleanRoomYieldImprovementMessagePack> Improvements { get; }

        public CleanRoomYieldDicingView(DicingOddsMessagePack odds, IReadOnlyList<CleanRoomYieldImprovementMessagePack> improvements)
        {
            Odds = odds;
            Improvements = improvements;
        }
    }
}
