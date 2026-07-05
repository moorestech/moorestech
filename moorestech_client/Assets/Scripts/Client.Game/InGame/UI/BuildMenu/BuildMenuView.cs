using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Master;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// 解放済みブロックのグリッドを表示する設置メニュー
    /// Build menu grid showing unlocked placeable blocks
    /// </summary>
    public class BuildMenuView : MonoBehaviour
    {
        [SerializeField] private RectTransform blockListContainer;

        [Inject] private IGameUnlockStateData _gameUnlockStateData;

        private readonly List<ItemSlotView> _slotViews = new();
        private BlockId? _clickedBlockId;

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
            if (active) RebuildBlockList();
        }

        public bool TryConsumeSelectedBlock(out BlockId selectedBlockId)
        {
            // クリック済み選択を1回だけ消費する（一方通行フロー）
            // Consume the clicked selection once (one-way flow)
            if (_clickedBlockId.HasValue)
            {
                selectedBlockId = _clickedBlockId.Value;
                _clickedBlockId = null;
                return true;
            }

            selectedBlockId = default;
            return false;
        }

        private void RebuildBlockList()
        {
            foreach (var slotView in _slotViews) Destroy(slotView.gameObject);
            _slotViews.Clear();
            _clickedBlockId = null;

            // 解放済みブロックをソート順に列挙してスロット生成
            // Enumerate unlocked blocks in sort order and create slots
            var unlockedBlocks = MasterHolder.BlockMaster.Blocks.Data
                .Where(IsUnlocked)
                .OrderBy(b => b.SortPriority ?? 0)
                .ThenBy(b => b.Name)
                .ToList();
            foreach (var blockMaster in unlockedBlocks)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid);
                var itemId = MasterHolder.BlockMaster.GetItemId(blockId);
                var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);

                var slotView = Instantiate(ItemSlotView.Prefab, blockListContainer);
                slotView.SetItem(itemView, 0, CreateToolTipText(blockMaster));
                slotView.OnLeftClickUp.Subscribe(_ => _clickedBlockId = blockId).AddTo(slotView);
                _slotViews.Add(slotView);
            }
        }

        private bool IsUnlocked(BlockMasterElement blockMaster)
        {
            return _gameUnlockStateData.BlockUnlockStateInfos.TryGetValue(blockMaster.BlockGuid, out var state) && state.IsUnlocked;
        }

        private static string CreateToolTipText(BlockMasterElement blockMaster)
        {
            var builder = new StringBuilder(blockMaster.Name);
            if (blockMaster.RequiredItems == null || blockMaster.RequiredItems.Length == 0) return builder.ToString();

            // ツールチップに建設コストを列挙する
            // List the construction cost in the tooltip
            foreach (var requiredItem in blockMaster.RequiredItems)
            {
                var itemName = MasterHolder.ItemMaster.GetItemMaster(requiredItem.ItemGuid).Name;
                builder.Append($"\n{itemName} x{requiredItem.Count}");
            }

            return builder.ToString();
        }
    }
}
