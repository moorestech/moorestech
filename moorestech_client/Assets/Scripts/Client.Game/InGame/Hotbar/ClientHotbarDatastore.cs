using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Game.Hotbar;
using UniRx;

namespace Client.Game.InGame.Hotbar
{
    /// <summary>
    ///     ホットバー割当参照モデル(非MonoBehaviour)
    ///     Client-side model (non-MonoBehaviour) holding the hotbar's 9 assignment slots
    /// </summary>
    public class ClientHotbarDatastore
    {
        public IReadOnlyList<Guid> Assignments => _assignments;
        public IObservable<Unit> OnAssignmentsChanged => _onAssignmentsChanged;

        private readonly Guid[] _assignments = new Guid[HotbarAssignmentDatastore.SlotCount];
        private readonly Subject<Unit> _onAssignmentsChanged = new();
        private int? _pendingSelectRequest;

        // 購読・初期データ応答から適用する（送信起点のローカル書き換えはしない）
        // Applied from the subscription/initial-data response only; never mutated by the send path locally
        public void ApplyAssignments(Guid[] assignments)
        {
            Array.Copy(assignments, _assignments, HotbarAssignmentDatastore.SlotCount);
            _onAssignmentsChanged.OnNext(Unit.Default);
        }

        // 楽観更新はせず、サーバーからの va:event:hotbarUpdate エコーで反映する
        // No optimistic update; the va:event:hotbarUpdate echo from the server applies the change
        public void RequestAssign(int slot, Guid targetId)
        {
            ClientContext.VanillaApi.SendOnly.AssignHotbar(slot, targetId);
        }

        public void RequestClear(int slot)
        {
            ClientContext.VanillaApi.SendOnly.ClearHotbar(slot);
        }

        public void RequestSwap(int slotA, int slotB)
        {
            ClientContext.VanillaApi.SendOnly.SwapHotbar(slotA, slotB);
        }

        // Web由来のキー/クリック選択を貯め、UIStateが1回だけ消費する（前例 BuildMenuView.TryConsumeSelectedEntry）
        // Queues a web-originated key/click selection for UIState to consume once (precedent: BuildMenuView.TryConsumeSelectedEntry)
        public void EnqueueSelectRequest(int slot)
        {
            _pendingSelectRequest = slot;
        }

        // UIState遷移で未消費の要求を捨てる。復帰後に古いクリックが建築モードを暴発させない
        // Drops an unconsumed request on a UIState transition so a stale click cannot fire build mode after returning
        public void ClearPendingSelectRequest()
        {
            _pendingSelectRequest = null;
        }

        public bool TryConsumeSelectRequest(out int slot)
        {
            if (_pendingSelectRequest.HasValue)
            {
                slot = _pendingSelectRequest.Value;
                _pendingSelectRequest = null;
                return true;
            }

            slot = default;
            return false;
        }
    }
}
