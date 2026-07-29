using System;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Player;
using Core.Master;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Inventory.Equipment
{
    /// <summary>
    ///     選択中の装備に応じて手持ち3Dモデルをロード/破棄する
    ///     Loads and disposes the held 3D model according to the selected equipment
    /// </summary>
    public class EquipmentHeldItemModel : IInitializable, IDisposable
    {
        private readonly LocalPlayerEquipment _localPlayerEquipment;

        private GameObject _currentGrabItem;
        private CancellationTokenSource _loadCancellationTokenSource;
        private LoadedAsset<GameObject> _currentLoadedAsset;
        private IDisposable _changedSubscription;
        private ItemId _currentItemId = ItemMaster.EmptyItemId;

        public EquipmentHeldItemModel(LocalPlayerEquipment localPlayerEquipment)
        {
            _localPlayerEquipment = localPlayerEquipment;
        }

        public void Initialize()
        {
            // 購読前に適用済みの初期データにも追従するため、購読直後に一度反映する
            // Reflect once right after subscribing so initial data applied before this still gets followed
            _changedSubscription = _localPlayerEquipment.OnChanged.Subscribe(_ => ApplySelectedItem());
            ApplySelectedItem();
        }

        private void ApplySelectedItem()
        {
            // スロット更新でも変更通知は飛ぶため、手持ちアイテムが実際に変わった時だけ再ロードする
            // Slot updates raise the notification too, so reload only when the held item actually changed
            var itemId = _localPlayerEquipment.SelectedItem.Id;
            if (itemId == _currentItemId) return;

            _currentItemId = itemId;
            UpdateAsync().Forget();

            #region Internal

            async UniTaskVoid UpdateAsync()
            {
                // 既存のロード処理をキャンセル
                // Cancel the in-flight load
                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource?.Dispose();
                _loadCancellationTokenSource = new CancellationTokenSource();

                // 既存のアイテムをクリーンアップ
                // Clean up the existing item
                if (_currentGrabItem != null)
                {
                    UnityEngine.Object.Destroy(_currentGrabItem.gameObject);
                    _currentGrabItem = null;
                }

                // Addressableリソースを解放
                // Release the Addressable resource
                _currentLoadedAsset?.Dispose();
                _currentLoadedAsset = null;

                if (itemId == ItemMaster.EmptyItemId) return;

                // Addressableロードは外部境界のため、失敗をここで隔離する
                // The Addressable load is an external boundary, so its failure is isolated here
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
                            PlayerSystemContainer.Instance.PlayerGrabItemManager.SetItem(_currentGrabItem, false, Vector3.zero, Quaternion.identity);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load hand grab model for item {itemId}: {e.Message}");
                }
            }

            #endregion
        }

        public void Dispose()
        {
            // 購読とキャンセルトークンソースをクリーンアップ
            // Clean up the subscription and the cancellation token source
            _changedSubscription?.Dispose();
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource?.Dispose();

            // Addressableリソースを解放
            // Release the Addressable resource
            _currentLoadedAsset?.Dispose();

            // ゲームオブジェクトを破棄
            // Destroy the game object
            if (_currentGrabItem != null)
            {
                UnityEngine.Object.Destroy(_currentGrabItem);
            }
        }
    }
}
