using System.Globalization;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    internal static class BeltTransportSpeedProfile
    {
        internal static string Fixed(double transitSeconds) => "fixed:" + Number(transitSeconds);

        // 停止中も種類由来の速度境界を保ち、同特性の曲がり・坂は結合する。
        // Retain configured speed boundaries while stopped and coalesce curves/slopes with identical behavior.
        internal static string Gear(double transitSeconds, double baseRpm, double minimumRpm) =>
            "gear:" + Number(transitSeconds) + ":" + Number(baseRpm) + ":" + Number(minimumRpm);

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
