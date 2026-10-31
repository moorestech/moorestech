namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     クリーンルームが内部の機械へ押し込む効果。稼働できる（クラス値あり）か、止まっている（理由だけ）かのどちらか
    ///     The effect a clean room pushes into interior machines: either operable (with class values) or halted (reason only)
    /// </summary>
    public readonly struct CleanRoomEffect
    {
        // 密閉部屋内かつ閾値行がOutでないときだけ Operable。止まる理由も区別して持つ
        // Operable only inside a sealed room whose row is not Out; the halt reason is kept distinct
        public readonly CleanRoomOperability Operability;

        // Operable のときだけ書かれる。読むのは TryGetClassYield だけ
        // Written only when Operable; only TryGetClassYield reads it
        private readonly CleanRoomClassYield _classYield;

        public bool CanOperate => Operability == CleanRoomOperability.Operable;

        private CleanRoomEffect(CleanRoomOperability operability, CleanRoomClassYield classYield)
        {
            Operability = operability;
            _classYield = classYield;
        }

        // 稼働できる部屋: クラス行の3値を持つ
        // An operable room carries the three values of its class row
        public static CleanRoomEffect Operable(CleanRoomClassYield classYield)
        {
            return new CleanRoomEffect(CleanRoomOperability.Operable, classYield);
        }

        // 部屋が未検出・未密閉で止まっている
        // Halted because no sealed room was detected
        public static CleanRoomEffect NotEnclosed()
        {
            return new CleanRoomEffect(CleanRoomOperability.NotEnclosed, default);
        }

        // 部屋はあるがクラスが足りず止まっている
        // Halted because the room's class is insufficient
        public static CleanRoomEffect ClassOut()
        {
            return new CleanRoomEffect(CleanRoomOperability.ClassOut, default);
        }

        // 部屋の効果が届く前の値。抽選側のデコレータと停止理由の状態の初期値はここから引く
        // The value before any room effect arrives; the draw decorator and the halt-reason state take their defaults from here
        public static CleanRoomEffect BeforeFirstPush()
        {
            return NotEnclosed();
        }

        // 稼働できるときだけクラス値を返す。止まっているときは false で、止まった理由（未密閉・クラス不足）は Operability を読む
        // Returns the class values only while operable; false while halted, and the halt reason (not enclosed or class out) is read from Operability
        public bool TryGetClassYield(out CleanRoomClassYield classYield)
        {
            classYield = _classYield;
            return CanOperate;
        }
    }
}
