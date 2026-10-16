using System.Threading;

namespace Client.PlaytestReceiver.Upload
{
    // 補完中の箱を配送が宣言しないよう、全走行と起動時補完を直列化する
    // Serialize startup completion and upload runs so a box is never declared mid-completion
    public static class PlaytestOutboxAccess
    {
        public static readonly SemaphoreSlim Gate = new(1, 1);
    }
}
