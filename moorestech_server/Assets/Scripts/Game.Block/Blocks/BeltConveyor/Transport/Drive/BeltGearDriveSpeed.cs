using System;
using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Transport.Drive
{
    // 歯車ベルコンの速度はマスタ速度×(現在RPM/基準RPM)を切り捨て、Coreの上限で頭打ちにする。RPM 0 は停止
    // A gear belt runs at master speed × (current RPM / base RPM), floored and capped at the Core limit; RPM 0 stops it
    public static class BeltGearDriveSpeed
    {
        // 歯車比の掛け合わせで生じるfloatの誤差で整数を割り込まないための下駄
        // Guard so float noise from chained gear ratios does not drop a whole number by one
        private const double FloatNoise = 1e-4;

        public static int Resolve(int baseSpeed, float currentRpm, float baseRpm)
        {
            if (currentRpm <= 0f || baseRpm <= 0f) return 0;
            var exact = baseSpeed * ((double)currentRpm / baseRpm);
            var speed = (int)Math.Floor(exact + FloatNoise);
            return Math.Min(speed, BeltConstants.MaxSpeed);
        }
    }
}
