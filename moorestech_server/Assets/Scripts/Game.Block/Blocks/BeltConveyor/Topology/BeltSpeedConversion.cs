using System;
using Core.BeltTransport;
using Core.Update;

namespace Game.Block.Blocks.BeltConveyor.Topology
{
    // マスタの「1秒に進むマス数」を、搬送が使う「1tickに進む距離(1マス=360)」へ換算する
    // Converts the master's cells-per-second into the transport's per-tick distance (one cell = 360)
    // 整数に換算できる値だけがマスタに載る(Core.MasterのBeltSpeedPerSecondValidatorが同じ式で起動時に弾く)
    // Only values that convert to a whole number reach the master (Core.Master's BeltSpeedPerSecondValidator rejects others at startup with the same formula)
    public static class BeltSpeedConversion
    {
        // 秒速は k/18 の循環小数をJSONに小数6桁で書くので、その丸め誤差を整数として受け入れる(Validatorと同じ値)
        // The per-second value is a repeating decimal k/18 written to six places in JSON, so that rounding error still counts as whole (same value as the validator)
        private const double WholeNumberTolerance = 1e-4;

        public static int ToPerTick(double cellsPerSecond)
        {
            var exact = cellsPerSecond * BeltConstants.ItemWidth / GameUpdater.TicksPerSecond;
            var distancePerTick = (int)Math.Round(exact);

            // 検証をすり抜けた非整数は丸めると決定論が崩れるので落とす
            // A non-whole value that slipped past validation would break determinism if rounded, so fail loudly
            if (WholeNumberTolerance <= Math.Abs(exact - distancePerTick))
                throw new InvalidOperationException($"beltSpeedPerSecond:{cellsPerSecond} is not a whole per-tick distance (exact {exact}, rounded {distancePerTick})");
            return distancePerTick;
        }
    }
}
