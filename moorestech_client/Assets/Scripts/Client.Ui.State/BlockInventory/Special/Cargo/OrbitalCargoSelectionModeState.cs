using Core.Master;

namespace Client.Ui.State.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 積荷の一覧を出すかの局所状態。本文の Presenter が持ち、作り直しと Detach で消える（前例 MachineSelectionModeState を MachineSectionPresenter が持つ形）
    /// Local state deciding whether the cargo list shows; the body presenter owns it and it resets on rebuild and Detach (as MachineSectionPresenter owns MachineSelectionModeState)
    /// </summary>
    public sealed class OrbitalCargoSelectionModeState
    {
        private bool _selectionOpened;
        private bool _hasRequest;
        private ItemId _requestedPayloadItemId;

        public void OpenSelection()
        {
            _selectionOpened = true;
            _hasRequest = false;
        }

        public void RememberRequest(ItemId payloadItemId)
        {
            // 押した種類を要求として覚え、サーバーの選択が一致したら一覧を閉じる（拒否なら開いたまま）
            // Remember the pressed kind; close once the server's selection matches (a rejection keeps it open)
            _hasRequest = true;
            _requestedPayloadItemId = payloadItemId;
        }

        // 一覧を出すか。要求が届いたらここで閉じ、その後のほかの人の選び直しでは開き直さない
        // Whether the list shows; a fulfilled request closes it here so a later reselection by someone else does not reopen it
        public bool ResolveShowsSelection(OrbitalCargoDetail cargo)
        {
            // 未選択は解放済みが 0 種類でも一覧（選んでから使う。ADR 0106 の 2・7）
            // Unselected always shows the list, even with zero unlocked kinds (select before use; ADR 0106 §2, §7)
            if (!cargo.IsSelected) return true;
            if (!_selectionOpened) return false;
            if (!_hasRequest || !cargo.IsSelectedChoice(_requestedPayloadItemId)) return true;
            _selectionOpened = false;
            _hasRequest = false;
            return false;
        }
    }
}
