using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem;
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
    /// 解放済みブロック/車両/接続具/BPのグリッド表示メニュー
    /// Build menu grid showing unlocked blocks, train cars, connect tools, and blueprints
    /// </summary>
    public class BuildMenuView : MonoBehaviour
    {
        [SerializeField] private RectTransform blockListContainer;

        [Inject] private IGameUnlockStateData _gameUnlockStateData;
        [Inject] private ClientBlueprintLibrary _blueprintLibrary;

        private readonly List<ItemSlotView> _slotViews = new();
        private readonly List<string> _displayedBlueprintNames = new();
        private BuildMenuEntry? _clickedEntry;

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
            if (!active) return;

            // 前回セッションの未消費クリックを破棄
            // Discard an unconsumed click from the previous session before showing
            _clickedEntry = null;

            // キャッシュで即表示後、BP更新時に再構築
            // Show the cached list immediately, then rebuild after the BP library refresh only if needed
            RebuildEntryList();
            RefreshBlueprintsAndRebuild().Forget();

            #region Internal

            async UniTask RefreshBlueprintsAndRebuild()
            {
                await _blueprintLibrary.Refresh(this.GetCancellationTokenOnDestroy());

                // 更新完了前にメニューが閉じられていたら再構築しない
                // Skip the rebuild when the menu was closed before the refresh finished
                if (!gameObject.activeSelf) return;

                // BP一覧が表示中と同一なら再構築せず、RTT中のクリックを握り潰さない
                // Skip the destructive rebuild when the BP list is unchanged so clicks in the RTT window survive
                if (_blueprintLibrary.Blueprints.Select(b => b.Name).SequenceEqual(_displayedBlueprintNames)) return;

                RebuildEntryList();
            }

            #endregion
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
            _displayedBlueprintNames.Clear();

            // カタログが組み立てたエントリ一覧からスロットを生成する
            // Create slots from the entries assembled by the catalog
            var entries = BuildMenuEntryCatalog.CreateEntries(_gameUnlockStateData, _blueprintLibrary);
            foreach (var entry in entries)
            {
                var slotView = Instantiate(ItemSlotView.Prefab, blockListContainer);

                // アイコン無しBPはテキスト表示
                // Blueprint entries have no icon, so display them as text only
                if (entry.IconView == null) slotView.SetTextOnly(entry.ToolTipText, entry.ToolTipText);
                else slotView.SetItem(entry.IconView, 0, entry.ToolTipText);

                slotView.OnLeftClickUp.Subscribe(_ => _clickedEntry = entry).AddTo(slotView);

                // BPエントリのみ右クリックで即削除
                // Only blueprint entries are deletable: right-click deletes immediately (no confirm dialog in v1)
                if (entry.EntryType == PlacementSelectionType.Blueprint)
                {
                    _displayedBlueprintNames.Add(entry.BlueprintName);
                    slotView.OnRightClickUp.Subscribe(_ => DeleteBlueprintAndRebuild(entry.BlueprintName).Forget()).AddTo(slotView);
                }

                _slotViews.Add(slotView);
            }

            // 再構築後も存在するエントリへの保留クリックは維持する
            // Keep a pending click when its entry still exists after the rebuild
            if (_clickedEntry.HasValue && !entries.Any(e => IsSameEntry(e, _clickedEntry.Value))) _clickedEntry = null;

            #region Internal

            async UniTask DeleteBlueprintAndRebuild(string blueprintName)
            {
                await _blueprintLibrary.DeleteBlueprint(blueprintName, this.GetCancellationTokenOnDestroy());

                // 成功時はキャッシュが最新全件に置き換わるため、そこから再構築する
                // On success the cache holds the refreshed full list, so rebuild from it
                if (gameObject.activeSelf) RebuildEntryList();
            }

            // ID系フィールドで同一性判定（アイコン参照は比較しない）
            // Judge selection identity by the ID fields (icon references are excluded)
            static bool IsSameEntry(BuildMenuEntry a, BuildMenuEntry b)
            {
                return a.EntryType == b.EntryType && a.BlockId == b.BlockId && a.TrainCarGuid == b.TrainCarGuid && a.ConnectPlaceMode == b.ConnectPlaceMode && a.BlueprintName == b.BlueprintName;
            }

            #endregion
        }
    }
}
