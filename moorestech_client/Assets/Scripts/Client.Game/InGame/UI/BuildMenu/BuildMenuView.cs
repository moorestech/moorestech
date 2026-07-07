using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Game.UnlockState;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// 解放済みブロック・車両・接続ツールのグリッドを表示する設置メニュー
    /// Build menu grid showing unlocked blocks, train cars, and connect tools
    /// </summary>
    public class BuildMenuView : MonoBehaviour
    {
        [SerializeField] private RectTransform blockListContainer;

        [Inject] private IGameUnlockStateData _gameUnlockStateData;

        private readonly List<ItemSlotView> _slotViews = new();
        private BuildMenuEntry? _clickedEntry;

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
            if (active) RebuildEntryList();
        }

        public bool TryConsumeSelectedEntry(out BuildMenuEntry entry)
        {
            // クリック済み選択を1回だけ消費する（一方通行フロー）
            // Consume the clicked selection once (one-way flow)
            if (_clickedEntry.HasValue)
            {
                entry = _clickedEntry.Value;
                _clickedEntry = null;
                return true;
            }

            entry = default;
            return false;
        }

        private void RebuildEntryList()
        {
            foreach (var slotView in _slotViews) Destroy(slotView.gameObject);
            _slotViews.Clear();
            _clickedEntry = null;

            // カタログが組み立てたエントリ一覧からスロットを生成する
            // Create slots from the entries assembled by the catalog
            var entries = BuildMenuEntryCatalog.CreateEntries(_gameUnlockStateData);
            foreach (var entry in entries)
            {
                var itemView = ClientContext.ItemImageContainer.GetItemView(entry.IconItemId);

                var slotView = Instantiate(ItemSlotView.Prefab, blockListContainer);
                slotView.SetItem(itemView, 0, entry.ToolTipText);
                slotView.OnLeftClickUp.Subscribe(_ => _clickedEntry = entry).AddTo(slotView);
                _slotViews.Add(slotView);
            }
        }
    }
}
