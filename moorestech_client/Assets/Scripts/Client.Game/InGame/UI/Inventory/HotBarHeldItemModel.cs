using System;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.PlayerInventory.Interface;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory
{
    /// <summary>
    /// ホットバー選択に応じて手持ち3Dモデルをロード/破棄する
    /// Loads and disposes the held 3D model according to the hotbar selection
    /// </summary>
    public class HotBarHeldItemModel
    {
        private readonly ILocalPlayerInventory _localPlayerInventory;

        private GameObject _currentGrabItem;
        private CancellationTokenSource _loadCancellationTokenSource;
        private LoadedAsset<GameObject> _currentLoadedAsset;

        public HotBarHeldItemModel(ILocalPlayerInventory localPlayerInventory)
        {
            _localPlayerInventory = localPlayerInventory;
        }

        public async UniTaskVoid UpdateAsync(int selectIndex)
        {
            // 既存のロード処理をキャンセル
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource?.Dispose();
            _loadCancellationTokenSource = new CancellationTokenSource();

            // 既存のアイテムをクリーンアップ
            if (_currentGrabItem != null)
            {
                UnityEngine.Object.Destroy(_currentGrabItem.gameObject);
                _currentGrabItem = null;
            }

            // Addressableリソースを解放
            _currentLoadedAsset?.Dispose();
            _currentLoadedAsset = null;

            var itemId = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(selectIndex)].Id;

            if (itemId == ItemMaster.EmptyItemId) return;

            try
            {
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                var token = _loadCancellationTokenSource.Token;

                // handGrabModelが設定されているかチェック
                // Check if handGrabModel is set
                if (!string.IsNullOrEmpty(itemMaster.AddressablePaths?.HandGrabModel))
                {
                    // Addressableからロード
                    // Load from Addressable
                    _currentLoadedAsset = await AddressableLoader.LoadAsync<GameObject>(itemMaster.AddressablePaths.HandGrabModel);

                    if (token.IsCancellationRequested) return;

                    if (_currentLoadedAsset?.Asset != null)
                    {
                        _currentGrabItem = UnityEngine.Object.Instantiate(_currentLoadedAsset.Asset);
                        PlayerSystemContainer.Instance.PlayerGrabItemManager.SetItem(_currentGrabItem, false);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load hand grab model for item {itemId}: {e.Message}");
            }
        }

        public void Dispose()
        {
            // キャンセルトークンソースをクリーンアップ
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource?.Dispose();

            // Addressableリソースを解放
            _currentLoadedAsset?.Dispose();

            // ゲームオブジェクトを破棄
            if (_currentGrabItem != null)
            {
                UnityEngine.Object.Destroy(_currentGrabItem);
            }
        }
    }
}
