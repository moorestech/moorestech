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
        /// <summary>
        ///     有効セルが1件以上あるか。空バッチを履歴へ積まないための関門
        ///     Whether the record has any cells; the gate that keeps empty batches out of the history
        /// </summary>
        bool HasCells { get; }

        UniTask UndoAsync(BlockGameObjectDataStore blockGameObjectDataStore);
    }
}
