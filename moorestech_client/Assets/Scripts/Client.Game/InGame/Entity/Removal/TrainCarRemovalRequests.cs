using Client.Network.API;
using Client.Network.API.Requests.Removal;
using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Entity.Removal
{
    // 拒否は要求ごとの応答で、成功は構造同期で確定する。要求同士は互いを待たない
    // Denials settle by each request's response and success by structure sync; requests never wait on each other
    public sealed class TrainCarRemovalRequests
    {
        private readonly TrainUnitClientCache _cache;
        private readonly ITrainCarRemovalSender _sender;
        private readonly TrainUnitTickState _ticks;
        private readonly List<Request> _pending = new();

        public TrainCarRemovalRequests(TrainUnitClientCache cache, TrainUnitTickState ticks, ITrainCarRemovalSender sender)
        {
            _cache = cache;
            _sender = sender;
            _ticks = ticks;
            cache.OnUnitApplied.Subscribe(_ => MarkChanged());
            cache.OnUnitRemoved.Subscribe(_ => MarkChanged());
            ticks.OnTickCompleted.Subscribe(CheckCompletedTick);
        }

        public IObservable<DeleteCompletion> Remove(TrainCarInstanceId id, object key)
        {
            var request = new Request(id, key);
            if (!Contains(id))
            {
                Debug.LogWarning($"[TrainDelete] target absent before send: {id}");
                Complete(request, DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.UnknownError);
                return request.Result;
            }

            // 送信して応答を待つ。期限を過ぎても応答か構造同期で後から確定させる
            // Send and await the response; past the window it still settles later by response or sync
            _pending.Add(request);
            request.Result.OnNext(new DeleteCompletion(key, DeleteCompletionKind.Pending, null));
            AwaitResponse(request).Forget();
            Observable.Timer(PacketExchangeManager.ResponseTimeout).Take(1).Subscribe(_ => ReportUnconfirmed(request));
            return request.Result;
        }

        private async UniTaskVoid AwaitResponse(Request request)
        {
            var response = await _sender.RemoveTrain(request.Id, Application.exitCancellationToken);
            if (!_pending.Contains(request)) return; // silent-ok: 構造同期で既に成功確定済み / 成功結果をUIへ通知済み

            // 応答が無いまま待機が終わった要求は未確認で閉じる
            // Close as unconfirmed when the wait ended without a response
            if (response == null)
            {
                Debug.LogWarning($"[TrainDelete] no response; result unconfirmed: {request.Id}");
                Finish(request, DeleteCompletionKind.Unconfirmed, LocalizationKeys.Ui.Delete.UnknownError);
                return;
            }
            if (response.IsSuccess) return; // silent-ok: 受理済み。成否の確定は構造同期を待つ / 期限超過は未確認としてUIに表示

            Debug.LogWarning($"[TrainDelete] denied: {request.Id} reason={response.FailureReason}");
            Finish(request, DeleteCompletionKind.Rejected, ToDenyReason(response.FailureReason));
        }

        private void ReportUnconfirmed(Request request)
        {
            if (!_pending.Contains(request)) return; // silent-ok: 既に完了した要求の期限 / 完了結果をUIへ通知済み
            Debug.LogWarning($"[TrainDelete] result not confirmed within the response window: {request.Id}");
            request.Result.OnNext(new DeleteCompletion(request.Key, DeleteCompletionKind.Unconfirmed, null));
        }

        private void MarkChanged()
        {
            foreach (var request in _pending)
            {
                request.Changed = true;
                request.ChangedTick = _ticks.GetTick();
            }
        }

        private void CheckCompletedTick(uint completedTick)
        {
            // 構造イベントを同tick分すべて適用し終えてから不在を成功とみなす
            // Treat absence as success only after all structure events of that tick are applied
            foreach (var request in _pending.ToArray())
            {
                if (!request.Changed || completedTick < request.ChangedTick) continue; // silent-ok: 全構造イベント適用まで待つ / 未確認は期限通知で表示
                if (!Contains(request.Id)) Finish(request, DeleteCompletionKind.Succeeded, null);
            }
        }

        private bool Contains(TrainCarInstanceId id)
        {
            foreach (var unit in _cache.Units.Values)
                foreach (var car in unit.Cars)
                    if (car.TrainCarInstanceId == id) return true;
            return false;
        }

        private void Finish(Request request, DeleteCompletionKind kind, LocalizationKey? reason)
        {
            _pending.Remove(request);
            Complete(request, kind, reason);
        }

        private static void Complete(Request request, DeleteCompletionKind kind, LocalizationKey? reason)
        {
            request.Result.OnNext(new DeleteCompletion(request.Key, kind, reason));
            request.Result.OnCompleted();
        }

        private static LocalizationKey ToDenyReason(RemoveTrainCarProtocol.RemoveTrainCarFailureReason reason)
        {
            return reason switch
            {
                RemoveTrainCarProtocol.RemoveTrainCarFailureReason.PlayerInventoryFull => LocalizationKeys.Ui.Delete.InventoryFull,
                _ => LocalizationKeys.Ui.Delete.UnknownError,
            };
        }

        private sealed class Request
        {
            internal readonly TrainCarInstanceId Id;
            internal readonly object Key;
            internal readonly ReplaySubject<DeleteCompletion> Result = new(1);
            internal bool Changed;
            internal uint ChangedTick;
            internal Request(TrainCarInstanceId id, object key) { Id = id; Key = key; }
        }
    }
}
