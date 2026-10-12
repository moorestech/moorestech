using System;
using Core.BeltTransport;
using Core.Update;

namespace Game.Block.Blocks.BeltConveyor.Topology
{
    // マスタの「1秒に進むマス数」を、搬送が使う「1tickに進む距離(1マス=256)」へ換算する
    // Converts the master's cells-per-second into the transport's per-tick distance (one cell = 256)
    // 整数に換算できる値だけがマスタに載る(Core.MasterのBeltSpeedPerSecondValidatorが同じ式で起動時に弾く)
    // Only values that convert to a whole number reach the master (Core.Master's BeltSpeedPerSecondValidator rejects others at startup with the same formula)
    public static class BeltSpeedConversion
    {
        private const double WholeNumberTolerance = 1e-6;

        public static bool TryToPerTick(double cellsPerSecond, out int distancePerTick)
        {
            var exact = cellsPerSecond * BeltConstants.ItemWidth / GameUpdater.TicksPerSecond;
            distancePerTick = (int)Math.Round(exact);
            return Math.Abs(exact - distancePerTick) < WholeNumberTolerance;
        }

        public static int ToPerTick(double cellsPerSecond)
        {
            TryToPerTick(cellsPerSecond, out var distancePerTick);
            return distancePerTick;
        }
    }
}
