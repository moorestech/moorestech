using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    /// <summary>
    ///     クリーンルームのアイテムハッチの毎秒の搬送上限が正で、搬送側のint換算に収まるか検証する
    ///     Validates that every clean room item hatch has a positive per-second throughput cap that fits the hatch's int conversion
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
                if (!(0 < hatchParam.MaxItemsPerSecond))
                {
                    logs += $"[BlockMaster] Name:{block.Name} has maxItemsPerSecond:{hatchParam.MaxItemsPerSecond} that is not positive\n";
                    continue;
                }

                // ハッチは搬送枠をintへ切り捨てるので、int.MaxValue超と無限大は桁あふれで0搬送になる。floatは2^31へ丸まるためdoubleで比べる
                // The hatch floors its allowance to int, so values above int.MaxValue or infinity overflow to zero transfers; compare as double because float rounds to 2^31
                if ((double)hatchParam.MaxItemsPerSecond <= int.MaxValue) continue;
                logs += $"[BlockMaster] Name:{block.Name} has maxItemsPerSecond:{hatchParam.MaxItemsPerSecond} that exceeds int.MaxValue\n";
            }
            return logs;
        }
    }
}
