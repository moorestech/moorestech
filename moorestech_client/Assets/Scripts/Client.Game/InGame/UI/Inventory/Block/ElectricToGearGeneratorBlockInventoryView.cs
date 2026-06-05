using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.ElectricToGear;
using Game.PlayerInventory.Interface.Subscription;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block
{
    /// <summary>
    /// 電力→ギア発電機UIのルートビュー。出力モードごとの行を生成し、サーバーのStateDetailと同期する。
    /// Root view for the electric-to-gear generator UI; builds per-output-mode rows and syncs with the server StateDetail.
    /// </summary>
    public class ElectricToGearGeneratorBlockInventoryView : MonoBehaviour, IBlockInventoryView
    {
        [SerializeField] private Transform rowsParent;
        [SerializeField] private ElectricToGearOutputModeRowView rowTemplate;
        [SerializeField] private ToggleGroup toggleGroup;
        [SerializeField] private Slider fulfillmentBar;
        [SerializeField] private TMP_Text consumedPowerText;
        [SerializeField] private TMP_Text statusText;

        // インベントリを持たないブロックなので ISubInventory 系は空実装
        // This block has no item inventory, so the ISubInventory members are empty
        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; } = new List<ItemSlotView>();
        public List<IItemStack> SubInventory { get; } = new();
        public int Count => 0;
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; } = null; // インベントリはないのでnullを入れておく

        private readonly List<ElectricToGearOutputModeRowView> _rows = new();
        private BlockGameObject _blockGameObject;
        private Vector3Int _blockPosition;
        private bool _isSending;
        private bool _initialized;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            _blockPosition = blockGameObject.BlockPosInfo.OriginalPos;

            // マスタの出力モード定義を取得。想定外の型ならエラー表示して中断する。
            // Fetch the output-mode master definitions; bail with an error if the param type is unexpected.
            var param = blockGameObject.BlockMasterElement.BlockParam as ElectricToGearGeneratorBlockParam;
            if (param == null)
            {
                statusText.text = "invalid block param";
                Debug.LogError("[ElectricToGearGeneratorBlockInventoryView] BlockParam is not ElectricToGearGeneratorBlockParam");
                return;
            }

            // 出力モードごとに行を生成し、Toggle操作で選択を送信できるよう購読する。
            // Build one row per output mode and subscribe so toggling sends a mode switch.
            for (var i = 0; i < param.OutputModes.Length; i++)
            {
                var mode = param.OutputModes[i];
                var row = Instantiate(rowTemplate, rowsParent);
                row.gameObject.SetActive(true);
                row.Build(i, mode.Rpm, mode.Torque, mode.RequiredPower, toggleGroup);
                row.OnSelectRequested.Subscribe(OnRowSelected).AddTo(row);
                _rows.Add(row);
            }

            // テンプレート行は複製元なので非表示にしておく。
            // The template row is only a clone source, so hide it.
            rowTemplate.gameObject.SetActive(false);
            statusText.text = "未同期 / not synced";
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;

            // StateDetail未到着時は default(null) が返るので未同期表示にする。
            // GetStateDetail returns default (null) before any state arrives, so show "not synced".
            var state = _blockGameObject.GetStateDetail<ElectricToGearGeneratorBlockStateDetail>(ElectricToGearGeneratorBlockStateDetail.BlockStateDetailKey);
            if (state == null)
            {
                statusText.text = "未同期 / not synced";
                return;
            }

            // 選択状態・充足率・消費電力をサーバー状態に合わせて反映する。
            // Reflect the selected index, fulfillment rate, and consumed power from the server state.
            statusText.text = string.Empty;
            for (var i = 0; i < _rows.Count; i++) _rows[i].SetSelectedWithoutNotify(i == state.SelectedIndex);
            fulfillmentBar.value = state.ElectricFulfillmentRate;
            consumedPowerText.text = $"{state.ConsumedElectricPower:0} W";
        }

        private void OnRowSelected(int index)
        {
            // 送信中の多重リクエストを防ぐ。完了後は次UpdateのStateDetailで表示が更新される。
            // Prevent overlapping requests; the display updates from StateDetail on the next Update.
            if (_isSending) return;
            SendAsync(index).Forget();

            #region Internal

            async UniTask SendAsync(int idx)
            {
                _isSending = true;
                var ct = this.GetCancellationTokenOnDestroy();
                await ClientContext.VanillaApi.Response.SetElectricToGearOutputMode(_blockPosition, idx, ct);
                if (this == null) return; // 破棄後は触らない / don't touch after destroy
                _isSending = false;
                // 表示は次 Update の StateDetail に従う（楽観更新しない）。
                // Display follows StateDetail next Update (no optimistic update).
            }

            #endregion
        }

        // インベントリを持たないため空実装
        // No item inventory, so these are no-ops
        public void UpdateItemList(List<IItemStack> items) { }
        public void UpdateInventorySlot(int slot, IItemStack item) { }

        public void DestroyUI()
        {
            Destroy(gameObject);
        }

#if UNITY_EDITOR
        // テスト用: 指定 index の選択操作をプログラムから起動する（行クリック相当）。
        // Test-only: drive a selection for the given index (equivalent to a row click).
        public void SelectModeForTest(int index)
        {
            OnRowSelected(index);
        }
#endif
    }
}
