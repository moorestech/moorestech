using System.Collections.Generic;
using UnityEngine;

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

        internal LinkedListNode<IBuildOperationRecord> Reserve()
        {
            // 通信中の操作順を履歴に先取りする
            // Reserve the operation's history order during network wait
            var reservation = _records.AddLast((IBuildOperationRecord)null);
            if (MaxHistoryCount < _records.Count) _records.RemoveFirst();
            return reservation;
        }

        internal void Complete(LinkedListNode<IBuildOperationRecord> reservation, IBuildOperationRecord record)
        {
            if (reservation.List != _records)
            {
                Debug.LogWarning("[BuildHistory] completed operation was already evicted");
                return;
            }
            reservation.Value = record;
        }

        internal void Cancel(LinkedListNode<IBuildOperationRecord> reservation)
        {
            if (reservation.List == _records) _records.Remove(reservation);
        }

        public bool TryPop(out IBuildOperationRecord record)
        {
            record = null;
            if (_records.Count == 0) return false;
            if (_records.Last.Value == null)
            {
                Debug.Log("[BuildHistory] undo waits for pending placement response");
                return false;
            }

            record = _records.Last.Value;
            _records.RemoveLast();
            return true;
        }
    }
}
