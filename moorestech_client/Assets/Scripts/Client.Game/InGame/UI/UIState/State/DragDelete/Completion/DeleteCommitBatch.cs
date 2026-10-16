using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Completion
{
    // 表示体より長生きする送信バッチ。成功した記録だけでUndoを完成する
    // A request batch outliving views completes undo using confirmed records only
    internal sealed class DeleteCommitBatch
    {
        private readonly BuildOperationHistory _history;
        private readonly IRemovalRestoreSender _sender;
        private readonly List<Entry> _entries = new();
        private readonly List<RemovedObjectCollector> _successful = new();
        private readonly Subject<DeleteCompletion> _completed = new();
        private LinkedListNode<IBuildOperationRecord> _reservation;
        private int _pendingRecords;
        public IObservable<DeleteCompletion> OnCompleted => _completed;

        internal DeleteCommitBatch(IReadOnlyList<IDeleteTarget> targets, BuildOperationHistory history, IRemovalRestoreSender sender)
        {
            _history = history;
            _sender = sender;
            // 全対象を送信前に採取し、同期応答でも他対象の情報を失わない
            // Capture every target before any send, including synchronous completion
            foreach (var target in targets)
            {
                var captured = new RemovedObjectCollector();
                target.CollectRemovedObjects(captured);
                var entry = new Entry(target, captured);
                _entries.Add(entry);
                if (entry.HasRecord) _pendingRecords++;
            }
            if (_pendingRecords > 0) _reservation = _history.Reserve();
        }

        internal void Send()
        {
            foreach (var entry in _entries)
            {
                // 完了を先に購読してから個別要求を送る
                // Subscribe before sending the individual request
                entry.Subscription = entry.Target.OnDeleteCompleted.Subscribe(result => Complete(entry, result));
            }
            foreach (var entry in _entries) entry.Target.Delete();
        }

        private void Complete(Entry entry, DeleteCompletion result)
        {
            if (result.Kind == DeleteCompletionKind.Pending) { _completed.OnNext(result); return; }
            if (entry.HasRecord && !entry.RecordCompleted)
            {
                entry.RecordCompleted = true;
                if (result.Kind == DeleteCompletionKind.Succeeded) _successful.Add(entry.Captured);
                _pendingRecords--;
                if (_pendingRecords == 0)
                {
                    var record = RemoveOperationRecord.CreateFromCollectors(_successful, _sender);
                    if (record.HasRemovedObjects) _history.Complete(_reservation, record);
                    else _history.Cancel(_reservation);
                }
            }
            if (result.Kind != DeleteCompletionKind.Succeeded) Debug.LogWarning($"[DeleteBatch] target={result.TargetKey} result={result.Kind} reason={result.DenyReason}");
            _completed.OnNext(result);
            if (result.Kind != DeleteCompletionKind.Unconfirmed) entry.Subscription.Dispose();
        }

        private sealed class Entry
        {
            internal IDisposable Subscription;
            internal bool RecordCompleted;
            internal readonly IDeleteTarget Target;
            internal readonly RemovedObjectCollector Captured;
            internal bool HasRecord => Captured.Objects.Count > 0 || Captured.UnrecordableCount > 0 || Captured.GetUnrecordableBlocks().Count > 0;
            internal Entry(IDeleteTarget target, RemovedObjectCollector captured) { Target = target; Captured = captured; }
        }
    }
}
