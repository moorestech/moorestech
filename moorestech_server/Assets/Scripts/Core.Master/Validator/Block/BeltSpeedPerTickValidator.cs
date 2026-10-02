using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator
{
    /// <summary>
    ///     ベルトの1tick速度がsegmentシミュレーションの許容範囲内か検証する
    ///     Validates that a belt's per-tick speed fits the segment simulation's accepted range
    /// </summary>
    internal static class BeltSpeedPerTickValidator
    {
        // Core.BeltTransportはCore.Masterを参照するため逆参照できない。BeltConstants.MaxSpeed（=ItemWidth/2）と一致させる
        // Core.BeltTransport references Core.Master, so it cannot be referenced back; keep equal to BeltConstants.MaxSpeed (=ItemWidth/2)
        private const int MinBeltSpeedPerTick = 1;
        private const int MaxBeltSpeedPerTick = 128;

        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is BeltConveyorBlockParam beltParam) logs += ValidateSpeed(block.Name, beltParam.BeltSpeedPerTick);
                if (block.BlockParam is GearBeltConveyorBlockParam gearBeltParam) logs += ValidateSpeed(block.Name, gearBeltParam.BeltSpeedPerTick);
            }
            return logs;
        }

        // 0以下は永久停止、MaxSpeed超はsegmentが速度設定で例外を投げるため起動時に弾く
        // Zero or less stalls forever and above MaxSpeed makes the segment throw on speed set, so reject at startup
        private static string ValidateSpeed(string blockName, int beltSpeedPerTick)
        {
            if (MinBeltSpeedPerTick <= beltSpeedPerTick && beltSpeedPerTick <= MaxBeltSpeedPerTick) return "";
            return $"[BlockMaster] Name:{blockName} has beltSpeedPerTick:{beltSpeedPerTick} outside {MinBeltSpeedPerTick}..{MaxBeltSpeedPerTick}\n";
        }
    }
}
