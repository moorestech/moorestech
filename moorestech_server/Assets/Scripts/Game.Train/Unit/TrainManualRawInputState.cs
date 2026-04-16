using MessagePack;

namespace Game.Train.Unit
{
    [MessagePackObject]
    public readonly struct TrainManualRawInputState
    {
        // サーバー側で最新入力を保持するための生入力表現
        // Raw input representation stored on the server as the latest input
        [Key(0)] public bool Forward { get; }
        [Key(1)] public bool Backward { get; }
        [Key(2)] public bool Left { get; }
        [Key(3)] public bool Right { get; }

        // 入力未指定を扱うための中立値
        // Neutral value used when no input is specified
        public static TrainManualRawInputState Neutral => new(false, false, false, false);

        public TrainManualRawInputState(bool forward, bool backward, bool left, bool right)
        {
            Forward = forward;
            Backward = backward;
            Left = left;
            Right = right;
        }
    }
}
