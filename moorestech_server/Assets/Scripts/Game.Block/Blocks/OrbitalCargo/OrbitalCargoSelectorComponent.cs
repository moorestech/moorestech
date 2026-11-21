using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface.Component;
using Game.UnlockState;
using UnityEngine;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.OrbitalCargo
{
    /// <summary>
    ///     発射台と拠点が共有する積荷の選択
    ///     - 運べて解放済みの種類だけを選ばせる（ADR 0106 3）
    ///     - 選び直しで外れた入れ口の中身を返す（ADR 0106 4）
    ///     - ロードでは解放を見ない（ADR 0106 3・6）
    ///     Cargo selection shared by pads and bases
    ///     - Allows only carried, unlocked kinds (ADR 0106 3)
    ///     - Refunds inlets dropped by a reselection (ADR 0106 4)
    ///     - Ignores unlock state on load (ADR 0106 3, 6)
    /// </summary>
    public class OrbitalCargoSelectorComponent : IOrbitalCargoSelectorComponent, IBlockSaveState
    {
        public const string SaveKeyName = "orbitalCargoSelection";
        public string SaveKey => SaveKeyName;
        public bool IsDestroy { get; private set; }

        public OrbitalCargoSelection Selection
        {
            get
            {
                CheckDestroy(this);
                return _state.Current;
            }
        }

        private readonly OrbitalCargoSelectionState _state;
        private readonly IOrbitalCargoInletSlots _inletSlots;
        private readonly IGameUnlockStateData _unlockState;

        internal OrbitalCargoSelectorComponent(OrbitalCargoSelectionState state, IOrbitalCargoInletSlots inletSlots, IGameUnlockStateData unlockState)
        {
            _state = state;
            _inletSlots = inletSlots;
            _unlockState = unlockState;
        }

        internal OrbitalCargoSelectorComponent(Dictionary<string, object> componentStates, OrbitalCargoSelectionState state, IOrbitalCargoInletSlots inletSlots, IGameUnlockStateData unlockState)
            : this(state, inletSlots, unlockState)
        {
            // 項目の無い旧セーブは未選択で読み、開発者へ1行残す（ADR 0106 6・7）
            // Read an old save without the item as unselected and leave one line for developers (ADR 0106 6, 7)
            if (!BlockComponentStateReader.TryRead<OrbitalCargoSelectionSaveJsonObject>(componentStates, SaveKey, out var save))
            {
                Debug.LogWarning("積荷の選択がセーブに無いため、未選択として読み込みます");
                return;
            }
            // silent-ok: 未選択で保存された台 / プレイヤーは状態表示の「積荷を選んでいない」で気づく
            if (save.SelectedCargoItemGuid == null) return;

            // マスタ変更で運べなくなった種類は未選択に戻す。解放状態は見ない
            // Fall back to unselected for a kind the current master no longer carries; unlock state is not checked
            if (!TryResolveCarried(save.SelectedCargoItemGuid, out var payloadItemId))
            {
                Debug.LogWarning($"セーブの積荷 {save.SelectedCargoItemGuid} は今のマスタでこの台が運ぶ積荷ではないため、未選択として読み込みます");
                return;
            }
            _state.SetCurrent(new SelectedOrbitalCargo(payloadItemId));

            #region Internal

            bool TryResolveCarried(string guidText, out ItemId itemId)
            {
                return OrbitalCargoItemGuid.TryParseExistingItemGuid(guidText, out itemId) && _inletSlots.CarriedPayloadItemIds.Contains(itemId);
            }

            #endregion
        }

        public OrbitalCargoSelectionResult SelectCargo(ItemId payloadItemId, IOpenableInventory refundInventory)
        {
            CheckDestroy(this);
            // 運べない種類と未解放の種類は選ばせない
            // Refuse kinds this site cannot carry and locked kinds
            if (!_inletSlots.CarriedPayloadItemIds.Contains(payloadItemId)) return Reject(OrbitalCargoSelectionResult.NotCarriedHere);
            if (!IsUnlocked()) return Reject(OrbitalCargoSelectionResult.Locked);
            // silent-ok: 同じ種類の選び直しは何も変えない / 状態詳細の選択は既に同じ値
            if (_state.Current.IsSelected(payloadItemId)) return OrbitalCargoSelectionResult.Success;

            // 先に選択を切り替えてから外れた種類を返す。返却中の在庫通知は新しい選択で評価される
            // Switch first, then refund the dropped kinds; inventory events during the refund see the new selection
            var selection = new SelectedOrbitalCargo(payloadItemId);
            _state.SetCurrent(selection);
            _inletSlots.RefundUnselectedPayloads(selection, refundInventory);
            return OrbitalCargoSelectionResult.Success;

            #region Internal

            bool IsUnlocked()
            {
                return _unlockState.ItemUnlockStateInfos.TryGetValue(payloadItemId, out var info) && info.IsUnlocked;
            }

            OrbitalCargoSelectionResult Reject(OrbitalCargoSelectionResult reason)
            {
                Debug.LogWarning($"積荷の選択を拒否しました 理由:{reason} 積荷:{payloadItemId}");
                return reason;
            }

            #endregion
        }

        public object GetSaveState()
        {
            CheckDestroy(this);
            return new OrbitalCargoSelectionSaveJsonObject { SelectedCargoItemGuid = _state.Current.ToItemGuidTextOrNull() };
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
