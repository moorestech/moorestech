using System;
using Client.Common.Asset;
using Client.Mod.Texture;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.DebugSystem.ItemSlot
{
    /// <summary>
    ///     デバッグUI（ItemSelectModal）専用のアイテムスロット表示
    ///     Item slot view used only by the debug UI (ItemSelectModal)
    /// </summary>
    public class ItemSlotView : MonoBehaviour
    {
        public static ItemSlotView Prefab { get; private set; }
        
        public IObservable<ItemSlotView> OnRightClickUp => commonSlotView.OnRightClickUp.Select(_ => this);
        public ItemViewData ItemViewData { get; private set; }
        
        [SerializeField] private CommonSlotView commonSlotView;
        
        public void SetItem(ItemViewData itemView, int count)
        {
            ItemViewData = itemView;
            
            if (itemView == null || itemView.IsEmpty) commonSlotView.SetViewClear();
            else commonSlotView.SetView(itemView.ItemImage, count != 0 ? count.ToString() : string.Empty);
        }
        
        public static async UniTask LoadItemSlotViewPrefab()
        {
            const string itemSlotViewPath = "Vanilla/UI/ItemSlotView";
            var prefab = await AddressableLoader.LoadAsyncDefault<GameObject>(itemSlotViewPath);
            Prefab = prefab.GetComponent<ItemSlotView>();
        }
    }
}
