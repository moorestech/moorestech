using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Localization;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Subscription;
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
        }

        private void RefreshBlockName()
        {
            blockNameText.text = Localize.GetContent(
                ContentLocalizationKeys.BlockName(_blockGameObject.BlockMasterElement.BlockGuid));
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
