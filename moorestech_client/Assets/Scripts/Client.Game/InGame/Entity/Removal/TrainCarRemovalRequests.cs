using Client.Network.API;
using Client.Network.API.Requests.Removal;
using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Game.Train.Unit;
using MessagePack;
using Mooresmaster.Localization.Generated;
using Server.Event.Notification;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Entity.Removal
{
    // IDを持たない拒否通知のため車両要求は一件ずつ確定する
    // Serialize car requests because their denial notification has no target id
    public sealed class TrainCarRemovalRequests
    {
        private readonly TrainUnitClientCache _cache;
        private readonly ITrainCarRemovalSender _sender;
        private readonly TrainUnitTickState _ticks;
        private readonly Queue<Request> _queue = new();
        private Request _pending;
        private bool _changed;
        private bool _unconfirmed;
        private uint _changedTick;

        public TrainCarRemovalRequests(TrainUnitClientCache cache, TrainUnitTickState ticks, IVanillaApiEvent events, ITrainCarRemovalSender sender)
        {
            _cache = cache;
            _sender = sender;
            _ticks = ticks;
            cache.OnUnitApplied.Subscribe(_ => MarkChanged());
            cache.OnUnitRemoved.Subscribe(_ => MarkChanged());
            ticks.OnTickCompleted.Subscribe(CheckCompletedTick);
            events.SubscribeEventResponse(NotificationService.EventTag, payload =>
            {
                var message = MessagePackSerializer.Deserialize<NotificationMessagePack>(payload);
                if (message.MessageId == "denied.removeTrainCarInventoryFull" && _pending != null)
                    Finish(DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.InventoryFull);
            });
        }

        public IObservable<DeleteCompletion> Remove(TrainCarInstanceId id, object key)
        {
            var request = new Request(id, key);
            _queue.Enqueue(request);
            if (_unconfirmed) request.Result.OnNext(new DeleteCompletion(key, DeleteCompletionKind.Unconfirmed, LocalizationKeys.Ui.Tooltip.DeleteTrainQueueHeld));
            StartNext();
            return request.Result;
        }

        private void StartNext()
        {
            if (_pending != null || _queue.Count == 0) return; // silent-ok: 処理中は直列キューで保持 / 結果待ち件数は削除UIに表示
            _pending = _queue.Dequeue();
            _changed = false;
            _unconfirmed = false;
            if (!Contains(_pending.Id))
            {
                Debug.LogWarning($"[TrainDelete] target absent before send: {_pending.Id}");
                Finish(DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.UnknownError);
                return;
            }
            _pending.Result.OnNext(new DeleteCompletion(_pending.Key, DeleteCompletionKind.Pending, null));
            _sender.RemoveTrain(_pending.Id);
            // 既存応答待ちの期限を参照して未確認を通知。遅延通知との誤対応を防ぎキューは保持する
            // Report unconfirmed after the shared response window, retaining the queue to avoid misattribution
            var request = _pending;
            Observable.Timer(PacketExchangeManager.ResponseTimeout).Take(1).Subscribe(_ =>
            {
                if (!ReferenceEquals(_pending, request)) return; // silent-ok: 既に完了した要求の期限 / 完了結果をUIへ通知済み
                ReportUnconfirmed();
            });
        }

        private void ReportUnconfirmed()
        {
            var request = _pending;
            _unconfirmed = true;
            Debug.LogWarning($"[TrainDelete] result unconfirmed; later car requests are held: {request.Id}");
            request.Result.OnNext(new DeleteCompletion(request.Key, DeleteCompletionKind.Unconfirmed, LocalizationKeys.Ui.Tooltip.DeleteTrainQueueHeld));
            foreach (var queued in _queue)
                queued.Result.OnNext(new DeleteCompletion(queued.Key, DeleteCompletionKind.Unconfirmed, LocalizationKeys.Ui.Tooltip.DeleteTrainQueueHeld));
        }

        private void MarkChanged()
        {
            if (_pending == null) return; // silent-ok: 撤去要求以外の通常構造変更 / 同期画面が変更を表示
            _changed = true;
            _changedTick = _ticks.GetTick();
        }

        private void CheckCompletedTick(uint completedTick)
        {
            if (_pending == null || !_changed || completedTick < _changedTick) return; // silent-ok: 全構造イベント適用まで待つ / 未確認は期限通知で表示
            if (!Contains(_pending.Id)) Finish(DeleteCompletionKind.Succeeded, null);
        }

        private bool Contains(TrainCarInstanceId id)
        {
            foreach (var unit in _cache.Units.Values)
                foreach (var car in unit.Cars)
                    if (car.TrainCarInstanceId == id) return true;
            return false;
        }

        private void Finish(DeleteCompletionKind kind, LocalizationKey? reason)
        {
            var finished = _pending;
            _pending = null;
            finished.Result.OnNext(new DeleteCompletion(finished.Key, kind, reason));
            finished.Result.OnCompleted();
            StartNext();
        }

        private sealed class Request
        {
            internal readonly TrainCarInstanceId Id;
            internal readonly object Key;
            internal readonly ReplaySubject<DeleteCompletion> Result = new(1);
            internal Request(TrainCarInstanceId id, object key) { Id = id; Key = key; }
        }
    }
}
