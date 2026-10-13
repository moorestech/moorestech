using System;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator
{
    /// <summary>
    ///     ベルトの秒速(1秒に進むマス数)が、1tickあたりの整数距離へ換算でき、搬送の許容範囲に収まるか検証する
    ///     Validates that a belt's cells-per-second converts to a whole per-tick distance within the transport's accepted range
    /// </summary>
    internal static class BeltSpeedPerSecondValidator
    {
        // Core.BeltTransport・Core.UpdateはCore.Masterを参照するため逆参照できない。BeltConstants.ItemWidth・MaxSpeed・GameUpdater.TicksPerSecondと一致させる
        // Core.BeltTransport and Core.Update reference Core.Master, so they cannot be referenced back; keep equal to BeltConstants.ItemWidth / MaxSpeed and GameUpdater.TicksPerSecond
        private const int CellLength = 256;
        private const int TicksPerSecond = 20;
        private const int MinDistancePerTick = 1;
        private const int MaxDistancePerTick = 128;
        private const double WholeNumberTolerance = 1e-6;

        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is IBeltSpeedParam beltSpeedParam) logs += ValidateSpeed(block.Name, beltSpeedParam.BeltSpeedPerSecond);
            }
            return logs;

            #region Internal

            // 整数にならない値は搬送の決定論を崩すので弾く。0以下は永久停止、上限超はsegmentが速度設定で例外を投げるため起動時に弾く
            // A value that is not a whole number breaks the transport's determinism; zero or less stalls forever and above the cap makes the segment throw, so all are rejected at startup
            string ValidateSpeed(string blockName, double cellsPerSecond)
            {
                var exact = cellsPerSecond * CellLength / TicksPerSecond;
                var distancePerTick = (int)Math.Round(exact);
                if (WholeNumberTolerance <= Math.Abs(exact - distancePerTick))
                    return $"[BlockMaster] Name:{blockName} has beltSpeedPerSecond:{cellsPerSecond} that is not a whole per-tick distance (x{CellLength}/{TicksPerSecond} = {exact})\n";
                if (MinDistancePerTick <= distancePerTick && distancePerTick <= MaxDistancePerTick) return "";
                return $"[BlockMaster] Name:{blockName} has beltSpeedPerSecond:{cellsPerSecond} = {distancePerTick} per tick, outside {MinDistancePerTick}..{MaxDistancePerTick}\n";
            }

            #endregion
        }
    }
}
