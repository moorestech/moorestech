namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     クリーンルーム内でのみ稼働し部屋から効果を受け取る機械コンポーネント
    ///     Machine component that operates only inside a clean room and receives its effect
    /// </summary>
    public interface ICleanRoomMachine : IBlockComponent
    {
        // 加工処理中のみ true になり aMachine 項として汚染に計上される
        // True only while processing; counted into the aMachine pollution term
        bool IsPolluting { get; }

        void SetCleanRoomEffect(CleanRoomEffect effect);
    }
}
