using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.UI.Inventory.Common;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// 解放済みブロック・車両・接続ツール・BPのグリッドを表示する設置メニュー
    /// Build menu grid showing unlocked blocks, train cars, connect tools, and blueprints
    /// </summary>
    public class BuildMenuView : MonoBehaviour
    {
        [SerializeField] private RectTransform blockListContainer;

        [Inject] private IGameUnlockStateData _gameUnlockStateData;
        [Inject] private ClientBlueprintLibrary _blueprintLibrary;

        private readonly List<ItemSlotView> _slotViews = new();
        private BuildMenuEntry? _clickedEntry;

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
            if (!active) return;

            // まずキャッシュで即表示し、BPライブラリ更新後に再構築する
            // Show the cached list immediately, then rebuild after the BP library refresh
            RebuildEntryList();
            RefreshBlueprintsAndRebuild().Forget();
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

        private async UniTask RefreshBlueprintsAndRebuild()
        {
            await _blueprintLibrary.Refresh(this.GetCancellationTokenOnDestroy());

            // 更新完了前にメニューが閉じられていたら再構築しない
            // Skip the rebuild when the menu was closed before the refresh finished
            if (gameObject.activeSelf) RebuildEntryList();
        }

        private void RebuildEntryList()
        {
            foreach (var slotView in _slotViews) Destroy(slotView.gameObject);
            _slotViews.Clear();
            _clickedEntry = null;

            // カタログが組み立てたエントリ一覧からスロットを生成する
            // Create slots from the entries assembled by the catalog
            var entries = BuildMenuEntryCatalog.CreateEntries(_gameUnlockStateData, _blueprintLibrary);
            foreach (var entry in entries)
            {
                var slotView = Instantiate(ItemSlotView.Prefab, blockListContainer);

                // アイコンの無いBPエントリはテキストのみで表示する
                // Blueprint entries have no icon, so display them as text only
                if (entry.IconView == null) slotView.SetTextOnly(entry.ToolTipText, entry.ToolTipText);
                else slotView.SetItem(entry.IconView, 0, entry.ToolTipText);

                slotView.OnLeftClickUp.Subscribe(_ => _clickedEntry = entry).AddTo(slotView);
                _slotViews.Add(slotView);
            }
        }
    }
}
