// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
using System;
using Client.Common.Asset;
using Client.Mod.Texture;
using Core.Master;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Common
{
    public class ItemSlotView : MonoBehaviour
    {
        public static ItemSlotView Prefab { get; private set; }
        
        public IObservable<(ItemSlotView, ItemUIEventType)> OnPointerEvent => commonSlotView.OnPointerEvent.Select(e => (this, e.Item2));
        public IObservable<ItemSlotView> OnLeftClickUp => commonSlotView.OnLeftClickUp.Select(_ => this);
        public IObservable<ItemSlotView> OnRightClickUp => commonSlotView.OnRightClickUp.Select(_ => this);
        public ItemViewData ItemViewData { get; private set; }
        public int Count { get; private set; }
        
        [SerializeField] private CommonSlotView commonSlotView;

        public void SetItem(ItemViewData itemView, int count)
        {
            ItemViewData = itemView;
            Count = count;

            if (itemView == null || itemView.IsEmpty) commonSlotView.SetViewClear();
            else commonSlotView.SetView(itemView.ItemImage, GetCountText(count));
        }

        // クラフト数の表示のみを更新
        // Update only craftable count text
        public void SetCount(int count)
        {
            Count = count;
            var countText = GetCountText(count);
            commonSlotView.SetCountText(countText);
        }
        
        private string GetCountText(int count)
        {
            return count != 0 ? count.ToString() : string.Empty;
        }

        public void SetSlotViewOption(CommonSlotViewOption slotOption)
        {
            commonSlotView.SetSlotViewOption(slotOption);
        }
        
        public void SetActive(bool active)
        {
            commonSlotView.SetActive(active);
        }


        public static async UniTask LoadItemSlotViewPrefab()
        {
            const string itemSlotViewPath = "Vanilla/UI/ItemSlotView";
            var prefab = await AddressableLoader.LoadAsyncDefault<GameObject>(itemSlotViewPath);
            Prefab = prefab.GetComponent<ItemSlotView>();
        }
    }
}
