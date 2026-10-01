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
        private readonly HashSet<Guid> _creating = new();
        private readonly HashSet<Guid> _missing = new();
        private readonly UniTaskCompletionSource _failed = new();
        public string FailureReason { get; private set; }
        public UniTask WaitForFailureAsync() => _failed.Task;
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
                else if (!_missing.Contains(pair.Key) && _creating.Add(pair.Key))
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
                var result = await _factory.CreateAsync(id, new ItemId(requested.ItemId), requested.Position);
                _creating.Remove(id);
                _pendingTasks.Remove(id);
                // 欠損GUIDを保持し、次tickで同じロードを繰り返さない。
                // Retain missing identities to avoid retrying the same load on subsequent ticks.
                if (!result.Succeeded)
                {
                    _missing.Add(id);
                    FailureReason = $"Belt item {id} could not be rendered: {result.FailureReason}";
                    UnityEngine.Debug.LogError(FailureReason);
                    _failed.TrySetResult();
                    return;
                }
                var view = result.View;
                if (!_desired.TryGetValue(id, out var latest))
                {
                    UnityEngine.Debug.Log($"Belt item {id} departed while its view was loading; discarded completed view.");
                    view.Destroy();
                    return;
                }
                if (latest.ItemId != requested.ItemId)
                {
                    view.Destroy();
                    throw new InvalidOperationException($"Belt identity {id} changed item type during creation.");
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
