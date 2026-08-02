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
    public class EquipmentHeldItemModel : IInitializable
    {
        private readonly LocalPlayerEquipment _localPlayerEquipment;

        private GameObject _currentGrabItem;
        private CancellationTokenSource _loadCancellationTokenSource;
        private LoadedAsset<GameObject> _currentLoadedAsset;
        private ItemId _currentItemId = ItemMaster.EmptyItemId;

        public EquipmentHeldItemModel(LocalPlayerEquipment localPlayerEquipment)
        {
            _localPlayerEquipment = localPlayerEquipment;
        }

        public void Initialize()
        {
            // 購読前に適用済みの初期データにも追従するため、購読直後に一度反映する
            // Reflect once right after subscribing so initial data applied before this still gets followed
            _localPlayerEquipment.OnSlotsOrSelectionChanged.Subscribe(_ => ApplySelectedItem());
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

                // handGrabModelが未設定のアイテムは手に何も持たない
                // Items without a handGrabModel hold nothing in hand
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                if (string.IsNullOrEmpty(itemMaster.AddressablePaths?.HandGrabModel)) return;

                var token = _loadCancellationTokenSource.Token;
                var loadedAsset = await AddressableLoader.LoadAsync<GameObject>(itemMaster.AddressablePaths.HandGrabModel, token);

                // 待機中に持ち替えられていたら、新しいロードの結果を上書きしないようここで解放して降りる
                // If the equipment changed while awaiting, release here and bail so a newer load's result is not clobbered
                if (token.IsCancellationRequested)
                {
                    loadedAsset?.Dispose();
                    return;
                }

                // ロード失敗はAddressableLoaderがログ済みでnullを返す
                // A failed load is already logged by AddressableLoader, which returns null
                if (loadedAsset?.Asset == null) return;

                _currentLoadedAsset = loadedAsset;
                _currentGrabItem = UnityEngine.Object.Instantiate(loadedAsset.Asset);
                PlayerSystemContainer.Instance.PlayerGrabItemManager.SetItem(_currentGrabItem, false, Vector3.zero, Quaternion.identity);
            }

            #endregion
        }
    }
}
