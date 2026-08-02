// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Localization;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Subscription;
using Mooresmaster.Localization.Generated;
using TMPro;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    /// <summary>
    ///     電柱インタラクト時に開く電力ネットワーク情報専用UI(インベントリなし)。
    ///     発電機UIと同じ ElectricNetworkInfoView を共有して情報表示を共通化する。
    ///     Dedicated electric-network-info UI (no inventory) opened when interacting with an electric pole;
    ///     shares the same ElectricNetworkInfoView as the generator UI to unify the display.
    /// </summary>
    public class ElectricPoleNetworkInfoUIView : MonoBehaviour, IBlockInventoryView
    {
        [SerializeField] private TMP_Text blockNameText;
        [SerializeField] private ElectricNetworkInfoView electricNetworkInfoView;

        private BlockGameObject _blockGameObject;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            RefreshBlockName();
            Localize.OnLanguageChanged.Subscribe(_ => RefreshBlockName()).AddTo(this);
            electricNetworkInfoView.Initialize(blockGameObject.BlockInstanceId);

            #region Internal

            void RefreshBlockName()
            {
                blockNameText.text = Localize.GetContent(
                    ContentLocalizationKeys.BlockName(_blockGameObject.BlockMasterElement.BlockGuid));
            }

            #endregion
        }

        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; } = new List<ItemSlotView>();
        public List<IItemStack> SubInventory { get; } = new();
        public int Count => 0;
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; } = null; // インベントリはないのでnullを入れておく

        public void UpdateItemList(List<IItemStack> response) { }
        public void UpdateInventorySlot(int slot, IItemStack item) { }
        public void DestroyUI()
        {
            Destroy(gameObject);
        }
    }
}
