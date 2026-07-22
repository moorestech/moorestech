using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     建築操作の履歴スタック。上限を超えたら最古を破棄するLIFO
    ///     Build operation history stack; LIFO that drops the oldest entry over capacity
    /// </summary>
    public class BuildOperationHistory
    {
        private const int MaxHistoryCount = 32;
        private readonly LinkedList<IBuildOperationRecord> _records = new();

        public void Push(IBuildOperationRecord record)
        {
            _records.AddLast(record);
            if (_records.Count > MaxHistoryCount) _records.RemoveFirst();
        }

        public bool TryPop(out IBuildOperationRecord record)
        {
            record = null;
            if (_records.Count == 0) return false;

            record = _records.Last.Value;
            _records.RemoveLast();
            return true;
        }
    }
}
