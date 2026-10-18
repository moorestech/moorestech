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
        private readonly Subject<SubInventoryModel> _onLoaded = new();
        private bool _pending;
        public bool IsLoading { get; private set; }
        public IObservable<SubInventoryModel> OnLoaded => _onLoaded;

        public SubInventoryLoader(ISubInventoryQuery query, ISubInventorySource source, CancellationToken ct)
        {
            _query = query;
            _source = source;
            _ct = ct;
        }

        public bool Matches(InventoryIdentifierMessagePack identifier)
        {
            var own = _source.InventoryIdentifier;
            if (own.InventoryType != identifier.InventoryType) return false;
            return own.InventoryType == InventoryType.Train
                ? own.TrainCarInstanceId == identifier.TrainCarInstanceId
                : own.BlockPosition.Vector3Int == identifier.BlockPosition.Vector3Int;
        }

        public void RequestRefresh()
        {
            // 取得中の複数通知は次の1要求へ集約する
            // Coalesce notifications during a load into the next request
            _pending = true;
            if (IsLoading) return; // silent-ok: 次の要求を予約済み / 成功完了後に取り直し、失敗時は次通知で再開する
            IsLoading = true;
            LoadPending().Forget();
        }

        private async UniTask LoadPending()
        {
            try
            {
                while (_pending)
                {
                    _pending = false;
                    var model = await _query.Load(_source, _ct);
                    _ct.ThrowIfCancellationRequested();
                    // 途中で更新された応答は表示せず最新を再取得する
                    // Refetch rather than display a response invalidated during its request
                    if (!_pending) _onLoaded.OnNext(model);
                }
            }
            finally
            {
                // 失敗はForgetへ伝え、次通知による再取得を可能にする
                // Propagate failures to Forget and allow the next notification to retry
                IsLoading = false;
            }
        }
    }
}
