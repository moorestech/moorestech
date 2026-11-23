using MessagePack;

namespace Server.Protocol.PacketResponse
{
    // EUV の最終の内訳（失敗の割合だけ）
    // EUV final odds (only the failure)
    [MessagePackObject]
    public class LayerExposureOddsMessagePack
    {
        [Key(0)] public double FailedRate { get; }

        [SerializationConstructor]
        public LayerExposureOddsMessagePack(double failedRate)
        {
            FailedRate = failedRate;
        }
    }

    // ダイシングの最終の内訳（出力個数・Lv別の最終割合・失敗。Lv別と失敗を足すと1）
    // Dicing final odds (output count, final per-level rates, failure; the levels plus the failure sum to 1)
    [MessagePackObject]
    public class DicingOddsMessagePack
    {
        [Key(0)] public int OutputCount { get; }
        [Key(1)] public double[] LevelRates { get; }
        [Key(2)] public double FailedRate { get; }

        [SerializationConstructor]
        public DicingOddsMessagePack(int outputCount, double[] levelRates, double failedRate)
        {
            OutputCount = outputCount;
            LevelRates = levelRates;
            FailedRate = failedRate;
        }
    }
}
