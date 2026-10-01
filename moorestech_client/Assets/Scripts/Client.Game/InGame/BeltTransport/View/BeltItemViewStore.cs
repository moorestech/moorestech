using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Game.InGame.Entity;
using Core.BeltTransport;
using Core.Master;
using Cysharp.Threading.Tasks;
namespace Client.Game.InGame.BeltTransport
{
    public sealed class BeltItemViewStore
    {
        private readonly IBeltItemViewFactory _factory;
        private readonly Dictionary<Guid, DesiredItem> _desired = new();
        private readonly Dictionary<Guid, IEntityObject> _views = new();
        private readonly Dictionary<Guid, Task> _pendingTasks = new();
        public BeltItemViewStore(IBeltItemViewFactory factory) { _factory = factory; }
        public async UniTask ApplyAsync(BeltNetworkSnapshot snapshot)
        {
            // tick境界の全GUIDを反映し、進行通知が無い停止アイテムも保持する。
            // Reconcile identities at tick boundaries and retain stopped items without expiry.
            _desired.Clear();
            foreach (var item in snapshot.Items) _desired.Add(item.Item.Guid, new DesiredItem(item.Item.ItemId, BeltItemPosition.Calculate(snapshot, item)));
            var removed = new List<Guid>();
            foreach (var pair in _views)
                if (!_desired.ContainsKey(pair.Key)) { pair.Value.Destroy(); removed.Add(pair.Key); }
            foreach (var id in removed) _views.Remove(id);

            // 同じGUIDの生成は一度だけ開始し、完了時に最新状態を再参照する。
            // Start each identity once and consult its latest state after asynchronous creation.
            var creations = new List<UniTask>();
            foreach (var pair in _desired)
            {
                if (_views.TryGetValue(pair.Key, out var view)) view.SetDirectPosition(pair.Value.Position);
                else if (!_pendingTasks.ContainsKey(pair.Key))
                {
                    // 起動待ちと更新側が同時に待てる共有Taskを一度だけ作る。
                    // Convert once to a shared Task for concurrent startup and update waiters.
                    var task = CreateAsync(pair.Key, pair.Value).AsTask();
                    if (!task.IsCompleted) _pendingTasks.Add(pair.Key, task);
                    creations.Add(task.AsUniTask());
                }
            }
            await UniTask.WhenAll(creations);

            #region Internal
            async UniTask CreateAsync(Guid id, DesiredItem requested)
            {
                var view = await _factory.CreateAsync(id, new ItemId(requested.ItemId), requested.Position);
                _pendingTasks.Remove(id);
                // 生成中に搬出されたアイテムを表示へ戻さない。
                // Do not restore a view for an item that departed during creation.
                if (!_desired.TryGetValue(id, out var latest))
                {
                    view.Destroy();
                    return;
                }
                view.SetDirectPosition(latest.Position);
                _views.Add(id, view);
            }
            #endregion
        }
        internal UniTask WaitForPendingAsync() => Task.WhenAll(_pendingTasks.Values).AsUniTask();

        private readonly struct DesiredItem
        {
            internal readonly int ItemId;
            internal readonly UnityEngine.Vector3 Position;
            internal DesiredItem(int itemId, UnityEngine.Vector3 position) { ItemId = itemId; Position = position; }
        }
    }
}
