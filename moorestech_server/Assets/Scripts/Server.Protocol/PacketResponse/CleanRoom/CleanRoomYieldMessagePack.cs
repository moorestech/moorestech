using System;
using MessagePack;

namespace Server.Protocol.PacketResponse
{
    // 成膜の抽選の中身（上限グレードと1段の昇格確率）
    // Deposition draw details (grade cap and per-step promotion chance)
    [MessagePackObject]
    public class WaferGradeYieldMessagePack
    {
        [Key(0)] public int MaxGrade { get; set; }
        [Key(1)] public double PromotionChance { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public WaferGradeYieldMessagePack() { }

        public WaferGradeYieldMessagePack(int maxGrade, double promotionChance)
        {
            MaxGrade = maxGrade;
            PromotionChance = promotionChance;
        }
    }

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

    // ダイシングの抽選の中身（ダイの損失率）
    // Dicing draw details (die loss rate)
    [MessagePackObject]
    public class DicingYieldMessagePack
    {
        [Key(0)] public double DieLossRate { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public DicingYieldMessagePack() { }

        public DicingYieldMessagePack(double dieLossRate)
        {
            DieLossRate = dieLossRate;
        }
    }

    // 応答の判別子。抽選の中身の種別を表す列挙はこれ1つだけ
    // The response discriminator; the only enum naming draw-detail kinds
    public enum CleanRoomYieldResponseKind
    {
        BlockNotFound,
        NoRole,
        NotOperable,
        WaferGrade,
        LayerExposure,
        Dicing,
        UnsupportedTarget,
    }
}
