using System;
using System.Threading;
using Client.Game.InGame.UI.Inventory;
using Cysharp.Threading.Tasks;
using Server.Util.MessagePack;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    internal sealed class SubInventoryLoader
    {
        private readonly ISubInventoryQuery _query;
        private readonly ISubInventorySource _source;
        private readonly CancellationToken _ct;
        private readonly Subject<SubInventoryModel> _onSubInventoryLoaded = new();
        private bool _pending;
        private bool _hasCurrentSnapshot;
        private bool _isLoading;
        public IObservable<SubInventoryModel> OnSubInventoryLoaded => _onSubInventoryLoaded;

        public SubInventoryLoader(ISubInventoryQuery query, ISubInventorySource source, CancellationToken ct)
        {
            _query = query;
            _source = source;
            _ct = ct;
        }

        public InventoryEventDisposition HandleEvent(InventoryIdentifierMessagePack identifier, InventoryEventType eventType)
        {
            if (_ct.IsCancellationRequested || !Matches(identifier)) return InventoryEventDisposition.Ignored; // silent-ok: 閉鎖済みや別対象 / Stateは開いた対象だけ更新する
            if (eventType == InventoryEventType.Remove) return InventoryEventDisposition.Remove;
            if (eventType == InventoryEventType.Update && _hasCurrentSnapshot) return InventoryEventDisposition.ApplyItemUpdate;
            if (eventType == InventoryEventType.Refresh || eventType == InventoryEventType.Update)
            {
                RequestRefresh();
                return InventoryEventDisposition.RefreshQueued;
            }
            return InventoryEventDisposition.Ignored; // silent-ok: 対象外イベント / 対応するイベントだけStateに渡す

            #region Internal

            bool Matches(InventoryIdentifierMessagePack identifier)
            {
                var own = _source.InventoryIdentifier;
                if (own.InventoryType != identifier.InventoryType) return false;
                return own.InventoryType == InventoryType.Train
                    ? own.TrainCarInstanceId == identifier.TrainCarInstanceId
                    : own.BlockPosition.Vector3Int == identifier.BlockPosition.Vector3Int;
            }

            #endregion
        }

        public void RequestRefresh()
        {
            // 取得中の複数通知は次の1要求へ集約する
            // Coalesce notifications during a load into the next request
            _hasCurrentSnapshot = false;
            _pending = true;
            if (_isLoading) return; // silent-ok: 次の要求を予約済み / 応答後に予約を消化し、新通知で再開する
            _isLoading = true;
            LoadPending().Forget();

            #region Internal

            async UniTask LoadPending()
            {
                try
                {
                    while (_pending)
                    {
                        _pending = false;
                        var model = await _query.Load(_source, _ct);
                        _ct.ThrowIfCancellationRequested();
                        // 各応答を公開し、鮮度が無効なら次要求だけ予約する
                        // Publish each response while reserving a newer snapshot separately
                        _hasCurrentSnapshot = !_pending && model.Result == Server.Protocol.PacketResponse.InventoryRequestResult.Success;
                        _onSubInventoryLoaded.OnNext(model);
                    }
                }
                finally
                {
                    // 失敗はForgetへ伝え、次通知による再取得を可能にする
                    // Propagate failures to Forget and allow the next notification to retry
                    _isLoading = false;
                }
            }

            #endregion
        }
    }

    internal enum InventoryEventDisposition
    {
        Ignored,
        RefreshQueued,
        ApplyItemUpdate,
        Remove,
    }
}
