using Client.Network.API.Requests.Removal;
using System;
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
    // 要求ごとの応答で成否を確定する。要求同士は互いを待たず、応答待ちは期限で必ず終わる
    // Each request settles by its own response; requests never wait on each other and every wait ends by deadline
    public sealed class TrainCarRemovalRequests
    {
        private readonly TrainUnitClientCache _cache;
        private readonly ITrainCarRemovalSender _sender;

        public TrainCarRemovalRequests(TrainUnitClientCache cache, ITrainCarRemovalSender sender)
        {
            _cache = cache;
            _sender = sender;
        }

        public IObservable<DeleteCompletion> Remove(TrainCarInstanceId id, object key)
        {
            var result = new ReplaySubject<DeleteCompletion>(1);
            if (!Contains(id))
            {
                Debug.LogWarning($"[TrainDelete] target absent before send: {id}");
                Complete(DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.UnknownError);
                return result;
            }

            result.OnNext(new DeleteCompletion(key, DeleteCompletionKind.Pending, null));
            AwaitResponse().Forget();
            return result;

            #region Internal

            async UniTaskVoid AwaitResponse()
            {
                var (response, reason) = await _sender.RemoveTrain(id, Application.exitCancellationToken);

                // 未確認は削除UIが保留扱いで車両を再選択不能にするため、期限切れ・復号失敗は不明な拒否で閉じる
                // Unconfirmed keeps the car unselectable in the delete UI, so close timeouts and decode failures as unknown denials
                if (response == null)
                {
                    Debug.LogWarning($"[TrainDelete] no usable response ({reason}); closing as rejected: {id}");
                    Complete(DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.UnknownError);
                    return;
                }

                // サーバーに対象が無い場合は撤去の目的を満たしているため成功とする
                // A target already absent on the server already meets the removal goal, so treat it as success
                if (response.IsSuccess || IsAlreadyAbsent(response.FailureReason))
                {
                    if (!response.IsSuccess) Debug.Log($"[TrainDelete] target already absent on server ({response.FailureReason}): {id}");
                    Complete(DeleteCompletionKind.Succeeded, null);
                    return;
                }

                Debug.LogWarning($"[TrainDelete] denied: {id} reason={response.FailureReason}");
                Complete(DeleteCompletionKind.Rejected, ToDenyReason(response.FailureReason));
            }

            void Complete(DeleteCompletionKind kind, LocalizationKey? denyReason)
            {
                result.OnNext(new DeleteCompletion(key, kind, denyReason));
                result.OnCompleted();
            }

            bool IsAlreadyAbsent(RemoveTrainCarProtocol.RemoveTrainCarFailureReason failure)
            {
                return failure == RemoveTrainCarProtocol.RemoveTrainCarFailureReason.TrainNotFound
                    || failure == RemoveTrainCarProtocol.RemoveTrainCarFailureReason.CarNotFound;
            }

            LocalizationKey ToDenyReason(RemoveTrainCarProtocol.RemoveTrainCarFailureReason failure)
            {
                return failure switch
                {
                    RemoveTrainCarProtocol.RemoveTrainCarFailureReason.PlayerInventoryFull => LocalizationKeys.Ui.Delete.InventoryFull,
                    _ => LocalizationKeys.Ui.Delete.UnknownError,
                };
            }

            #endregion
        }

        private bool Contains(TrainCarInstanceId id)
        {
            foreach (var unit in _cache.Units.Values)
                foreach (var car in unit.Cars)
                    if (car.TrainCarInstanceId == id) return true;
            return false;
        }
    }
}
