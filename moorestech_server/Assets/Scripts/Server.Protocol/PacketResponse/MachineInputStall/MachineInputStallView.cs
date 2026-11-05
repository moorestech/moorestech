namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     止まった理由の応答を種別ごとの型で読む口。BlockNotFound はこの型に入らず、取得側（ポーラー）が扱う
    ///     Per-kind read view of the stall response; BlockNotFound is not part of it and is handled by the fetcher (poller)
    /// </summary>
    public abstract class MachineInputStallView
    {
        // 種別はこのファイルの型に閉じる
        // The kinds are closed to the types in this file
        private protected MachineInputStallView()
        {
        }
    }

    public sealed class MachineInputNotStalledView : MachineInputStallView
    {
        public static readonly MachineInputNotStalledView Instance = new();

        private MachineInputNotStalledView()
        {
        }
    }

    public sealed class MachineInputStalledView : MachineInputStallView
    {
        public MachineInputStallMessagePack Stall { get; }

        public MachineInputStalledView(MachineInputStallMessagePack stall)
        {
            Stall = stall;
        }
    }
}
