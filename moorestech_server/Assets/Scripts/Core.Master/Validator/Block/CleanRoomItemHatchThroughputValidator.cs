using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    /// <summary>
    ///     クリーンルームのアイテムハッチの毎秒の搬送上限が正の値か検証する
    ///     Validates that every clean room item hatch has a positive per-second throughput cap
    /// </summary>
    internal static class CleanRoomItemHatchThroughputValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is not CleanRoomItemHatchBlockParam hatchParam) continue;

                // 0以下だとハッチが何も通さなくなるので起動時に弾く
                // Zero or less would let nothing through the hatch, so reject it at startup
                if (0 < hatchParam.MaxItemsPerSecond) continue;
                logs += $"[BlockMaster] Name:{block.Name} has maxItemsPerSecond:{hatchParam.MaxItemsPerSecond} that is not positive\n";
            }
            return logs;
        }
    }
}
