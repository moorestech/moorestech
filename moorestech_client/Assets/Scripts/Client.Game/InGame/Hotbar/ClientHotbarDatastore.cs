using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Server.Protocol.PacketResponse;
using UniRx;

namespace Client.Game.InGame.Hotbar
{
    /// <summary>
    ///     ホットバー9枠の割当参照と選択枠を保持するクライアントモデル（非MonoBehaviour）
    ///     Client-side model (non-MonoBehaviour) holding the hotbar's 9 assignment slots and the selected slot
    /// </summary>
    public class ClientHotbarDatastore
    {
        // サーバー側 Game.Hotbar.HotbarAssignmentDatastore.SlotCount と同値
        // Matches Game.Hotbar.HotbarAssignmentDatastore.SlotCount on the server
        private const int SlotCount = 9;

        public IReadOnlyList<Guid> Assignments => _assignments;
        public int SelectedSlot { get; private set; } = -1;
        public IObservable<Unit> OnChanged => _onChanged;

        private readonly Guid[] _assignments = new Guid[SlotCount];
        private readonly Subject<Unit> _onChanged = new();
        private int? _pendingSelectRequest;

        // 購読・初期データ応答から適用する（送信起点のローカル書き換えはしない）
        // Applied from the subscription/initial-data response only; never mutated by the send path locally
        public void ApplyAssignments(Guid[] assignments)
        {
            Array.Copy(assignments, _assignments, SlotCount);
            _onChanged.OnNext(Unit.Default);
        }

        public void SetSelectedSlot(int slot)
        {
            SelectedSlot = slot;
            _onChanged.OnNext(Unit.Default);
        }

        // 楽観更新はせず、サーバーからの va:event:hotbarUpdate エコーで反映する
        // No optimistic update; the va:event:hotbarUpdate echo from the server applies the change
        public void RequestAssign(int slot, Guid targetId)
        {
            var request = HotbarProtocol.HotbarProtocolMessagePack.CreateAssignRequest(ClientContext.PlayerConnectionSetting.PlayerId, slot, targetId);
            ClientContext.VanillaApi.SendOnly.SendHotbarRequest(request);
        }

        public void RequestClear(int slot)
        {
            var request = HotbarProtocol.HotbarProtocolMessagePack.CreateClearRequest(ClientContext.PlayerConnectionSetting.PlayerId, slot);
            ClientContext.VanillaApi.SendOnly.SendHotbarRequest(request);
        }

        public void RequestSwap(int slotA, int slotB)
        {
            var request = HotbarProtocol.HotbarProtocolMessagePack.CreateSwapRequest(ClientContext.PlayerConnectionSetting.PlayerId, slotA, slotB);
            ClientContext.VanillaApi.SendOnly.SendHotbarRequest(request);
        }

        // Web由来のキー/クリック選択を貯め、UIStateが1回だけ消費する（前例 BuildMenuView.TryConsumeSelectedEntry）
        // Queues a web-originated key/click selection for UIState to consume once (precedent: BuildMenuView.TryConsumeSelectedEntry)
        public void EnqueueSelectRequest(int slot)
        {
            _pendingSelectRequest = slot;
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
