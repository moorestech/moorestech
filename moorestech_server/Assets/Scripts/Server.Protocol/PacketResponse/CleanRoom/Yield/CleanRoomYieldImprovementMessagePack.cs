using System;
using System.Collections.Generic;
using MessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     「失敗率の改善方法」の1行。組み立ては種別ごとの static factory、種別ごとの値の読み出しは TryGet に閉じる
    ///     One row of the improvement list; built only through per-kind static factories and read only through the TryGet methods
    /// </summary>
    [MessagePackObject]
    public class CleanRoomYieldImprovementMessagePack
    {
        [Key(0)] public CleanRoomYieldImprovementKind Kind { get; }

        // TargetClassName は RoomClassUp、ModuleTier は AddQualityModule のときだけ書かれる
        // TargetClassName is written only for RoomClassUp and ModuleTier only for AddQualityModule
        [Key(1)] public string TargetClassName { get; }
        [Key(2)] public int ModuleTier { get; }

        // 今との差分（仮の条件 − 今）。ダイシングは最終の内訳と同じ長さ、EUV は空
        // Difference from now (hypothetical minus current); dicing matches the final odds' length, EUV is empty
        [Key(3)] public double[] LevelRateDeltas { get; }
        [Key(4)] public double FailedRateDelta { get; }

        [SerializationConstructor]
        public CleanRoomYieldImprovementMessagePack(CleanRoomYieldImprovementKind kind, string targetClassName, int moduleTier, double[] levelRateDeltas, double failedRateDelta)
        {
            Kind = kind;
            TargetClassName = targetClassName;
            ModuleTier = moduleTier;
            LevelRateDeltas = levelRateDeltas;
            FailedRateDelta = failedRateDelta;
        }

        public static CleanRoomYieldImprovementMessagePack CreateRoomClassUp(string targetClassName, double[] levelRateDeltas, double failedRateDelta)
        {
            return new CleanRoomYieldImprovementMessagePack(CleanRoomYieldImprovementKind.RoomClassUp, targetClassName, 0, levelRateDeltas, failedRateDelta);
        }

        public static CleanRoomYieldImprovementMessagePack CreateAddQualityModule(int moduleTier, double[] levelRateDeltas, double failedRateDelta)
        {
            return new CleanRoomYieldImprovementMessagePack(CleanRoomYieldImprovementKind.AddQualityModule, null, moduleTier, levelRateDeltas, failedRateDelta);
        }

        public bool TryGetRoomClassUp(out string targetClassName)
        {
            targetClassName = Kind == CleanRoomYieldImprovementKind.RoomClassUp ? TargetClassName : default;
            return Kind == CleanRoomYieldImprovementKind.RoomClassUp;
        }

        public bool TryGetAddQualityModule(out int moduleTier)
        {
            moduleTier = Kind == CleanRoomYieldImprovementKind.AddQualityModule ? ModuleTier : default;
            return Kind == CleanRoomYieldImprovementKind.AddQualityModule;
        }

        // 外から届いた一覧が読めるか。各行の判別子・RoomClassUp のクラス名・Lv別差分の長さ（最終の内訳のLv数）を確かめる。検査はここ1か所で、読み口が呼び、クライアントで要るときもこれを呼ぶ
        // Whether a list from outside is readable: each row's discriminator, the RoomClassUp class name and the per-level delta length (the final odds' level count); the only copy of this check, called by the reader and by the client whenever it needs one
        public static bool AreReadable(IReadOnlyList<CleanRoomYieldImprovementMessagePack> improvements, int levelCount)
        {
            if (improvements == null) return false;
            foreach (var improvement in improvements)
            {
                if (improvement == null || improvement.LevelRateDeltas == null) return false;
                if (improvement.LevelRateDeltas.Length != levelCount) return false;
                if (!Enum.IsDefined(typeof(CleanRoomYieldImprovementKind), improvement.Kind)) return false;
                if (improvement.Kind == CleanRoomYieldImprovementKind.AddQualityModule && improvement.ModuleTier < 1) return false;
                if (improvement.Kind == CleanRoomYieldImprovementKind.RoomClassUp && string.IsNullOrWhiteSpace(improvement.TargetClassName)) return false;
            }
            return true;
        }
    }

    // 改善の種別。部屋クラスを1つ上げる／品質モジュールを1個足す
    // Improvement kinds: raise the room class by one, or add one quality module
    public enum CleanRoomYieldImprovementKind
    {
        RoomClassUp,
        AddQualityModule,
    }
}
