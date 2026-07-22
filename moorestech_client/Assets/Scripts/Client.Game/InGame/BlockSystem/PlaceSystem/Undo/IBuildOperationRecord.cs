using Client.Game.InGame.Block;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     建築操作履歴の1エントリ。取り消しの具体処理は各レコード自身が持つ
    ///     One entry of build operation history; each record encapsulates its own undo logic
    /// </summary>
    public interface IBuildOperationRecord
    {
        UniTask UndoAsync(BlockGameObjectDataStore blockGameObjectDataStore);
    }
}
