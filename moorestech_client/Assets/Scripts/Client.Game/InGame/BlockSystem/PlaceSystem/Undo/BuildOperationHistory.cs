using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     上限付きLIFOの建築操作履歴
    ///     Capped LIFO build-operation history
    /// </summary>
    public class BuildOperationHistory
    {
        private const int MaxHistoryCount = 32;
        private readonly LinkedList<IBuildOperationRecord> _records = new();

        public void Push(IBuildOperationRecord record)
        {
            _records.AddLast(record);
            if (MaxHistoryCount < _records.Count) _records.RemoveFirst();
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
