using Client.Ui.State.BlockInventory.Context;

namespace Client.Tests.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 「失敗率の改善方法」を開いた回数を記録する偽物
    /// Fake that records how many times the improvement modal was opened
    /// </summary>
    public sealed class RecordingCleanRoomYieldImprovementCommands : ICleanRoomYieldImprovementCommands
    {
        public int OpenCount { get; private set; }

        public void Open()
        {
            OpenCount++;
        }
    }
}
