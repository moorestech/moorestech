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

    public sealed class CleanRoomYieldWaferGradeView : CleanRoomYieldView
    {
        public WaferGradeYieldMessagePack WaferGrade { get; }

        // 出力待ちの確定済み抽選に使った値。待っていなければ null
        // Values of the fixed draw waiting for output; null when nothing waits
        public WaferGradeYieldMessagePack PendingDraw { get; }

        public CleanRoomYieldWaferGradeView(WaferGradeYieldMessagePack waferGrade, WaferGradeYieldMessagePack pendingDraw)
        {
            WaferGrade = waferGrade;
            PendingDraw = pendingDraw;
        }
    }

    public sealed class CleanRoomYieldLayerExposureView : CleanRoomYieldView
    {
        public LayerExposureYieldMessagePack LayerExposure { get; }

        // 出力待ちの確定済み抽選に使った値。待っていなければ null
        // Values of the fixed draw waiting for output; null when nothing waits
        public LayerExposureYieldMessagePack PendingDraw { get; }

        public CleanRoomYieldLayerExposureView(LayerExposureYieldMessagePack layerExposure, LayerExposureYieldMessagePack pendingDraw)
        {
            LayerExposure = layerExposure;
            PendingDraw = pendingDraw;
        }
    }

    public sealed class CleanRoomYieldDicingView : CleanRoomYieldView
    {
        public DicingYieldMessagePack Dicing { get; }

        // 出力待ちの確定済み抽選に使った値。待っていなければ null
        // Values of the fixed draw waiting for output; null when nothing waits
        public DicingYieldMessagePack PendingDraw { get; }

        public CleanRoomYieldDicingView(DicingYieldMessagePack dicing, DicingYieldMessagePack pendingDraw)
        {
            Dicing = dicing;
            PendingDraw = pendingDraw;
        }
    }
}
