using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    /// <summary>
    ///     清浄室機械の今の部屋の効果の読み口。書き換えと抽選は持たず、表示の部品（B6）はこの型でだけ受ける
    ///     Read view of a clean-room machine's current room effect; it cannot write or draw, and display components (B6) take only this type
    /// </summary>
    public interface ICleanRoomEffectLookup
    {
        CleanRoomEffect CurrentEffect { get; }
    }
}
