using System;
using MessagePack;

namespace Server.Protocol.PacketResponse
{
    // EUVの抽選の中身（層の失敗率）
    // EUV draw details (layer fail rate)
    [MessagePackObject]
    public class LayerExposureYieldMessagePack
    {
        [Key(0)] public double LayerFailRate { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public LayerExposureYieldMessagePack() { }

        public LayerExposureYieldMessagePack(double layerFailRate)
        {
            LayerFailRate = layerFailRate;
        }
    }

    // ダイシングの抽選の中身（ダイの損失率と、損失を除いた良品に対するレベル別割合・失敗ダイ割合）
    // Dicing draw details (die loss rate plus per-level and failed-die rates over survivors)
    [MessagePackObject]
    public class DicingYieldMessagePack
    {
        [Key(0)] public double DieLossRate { get; set; }
        [Key(1)] public int QualityTier { get; set; }
        [Key(2)] public double[] LevelRates { get; set; }
        [Key(3)] public double FailedRate { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public DicingYieldMessagePack() { }

        public DicingYieldMessagePack(double dieLossRate, int qualityTier, double[] levelRates, double failedRate)
        {
            DieLossRate = dieLossRate;
            QualityTier = qualityTier;
            LevelRates = levelRates;
            FailedRate = failedRate;
        }
    }

    // 応答の判別子。抽選の中身の種別を表す列挙はこれ1つだけ
    // The response discriminator; the only enum naming draw-detail kinds
    public enum CleanRoomYieldResponseKind
    {
        BlockNotFound,
        NoRole,
        NotOperable,
        LayerExposure,
        Dicing,
        UnsupportedTarget,
    }
}
