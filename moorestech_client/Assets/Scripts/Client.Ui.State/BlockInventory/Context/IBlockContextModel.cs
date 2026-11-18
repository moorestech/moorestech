using UniRx;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 追跡ブロックの文脈の 3 欄（部屋・抽選・詰まり）
    /// The tracked block's three context entries (room, draw, stall)
    /// </summary>
    public interface IBlockContextModel
    {
        IReadOnlyReactiveProperty<BlockContextViewModel> Current { get; }
    }
}
