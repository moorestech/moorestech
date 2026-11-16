namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     クリーンルーム内でのみ稼働し部屋から効果を受け取る機械コンポーネント
    ///     Machine component that operates only inside a clean room and receives its effect
    /// </summary>
    public interface ICleanRoomMachine : IBlockComponent
    {
        void SetCleanRoomEffect(CleanRoomEffect effect);
    }
}
