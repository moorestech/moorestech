using MessagePack;

namespace Game.Train.Unit
{
    [MessagePackObject]
    public readonly struct TrainManualCommand
    {
        // 生入力を列車ユニット向けの適用可能な指示へ変換した結果
        // Result of converting raw input into an applicable unit command
        [Key(0)] public int MasconLevel { get; }
        [Key(1)] public bool ShouldReverseUnit { get; }

        // 操作なしを明示するための中立コマンド
        // Neutral command used to represent no manual operation
        public static TrainManualCommand Neutral => new(0, false);

        public TrainManualCommand(int masconLevel, bool shouldReverseUnit)
        {
            MasconLevel = masconLevel;
            ShouldReverseUnit = shouldReverseUnit;
        }
    }
}
