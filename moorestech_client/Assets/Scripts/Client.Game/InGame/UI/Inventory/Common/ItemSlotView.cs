using System;
using Client.Common.Asset;
using Client.Localization;
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

        private bool _usesDefaultToolTip;

        private void Awake()
        {
            Localize.OnLanguageChanged
                .Subscribe(_ => RefreshDefaultToolTip())
                .AddTo(this);
        }

        public void SetItem(ItemViewData itemView, int count)
        {
            SetItem(itemView, count, null);
        }

        public void SetItem(ItemViewData itemView, int count, string toolTipText)
        {
            ItemViewData = itemView;
            Count = count;
            _usesDefaultToolTip = toolTipText == null;

            if (itemView == null || itemView.IsEmpty)
            {
                commonSlotView.SetViewClear();
            }
            else
            {
                if (_usesDefaultToolTip)
                {
                    toolTipText = GetToolTipText(itemView);
                    if (string.IsNullOrEmpty(toolTipText))
                    {
                        commonSlotView.SetView(itemView.ItemImage, GetCountText(count), string.Empty);
                        commonSlotView.SetShowToolTip(false);
                        return;
                    }
                }
                
                commonSlotView.SetView(itemView.ItemImage, GetCountText(count), toolTipText);
            }
        }

        // 液体出力のみのレシピ表示用に液体アイコンを表示
        // Display a fluid icon for recipes whose output is fluid only
        public void SetFluid(FluidViewData fluidView, string toolTipText)
        {
            ItemViewData = null;
            Count = 0;
            commonSlotView.SetView(fluidView.FluidImage, string.Empty, toolTipText);
        }

        // アイコン無しエントリはテキストのみ表示（BP等）
        // Display an icon-less entry as text only (e.g. blueprint entries)
        public void SetTextOnly(string text, string toolTipText)
        {
            ItemViewData = null;
            Count = 0;
            commonSlotView.SetViewTextOnly(text, toolTipText);
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
        
        
        public static string GetToolTipText(ItemViewData itemView)
        {
            // マスタに紐づかない表示名は辞書キーを持たないため既定Tooltipへ公開しない
            // Do not expose names without a master-backed dictionary key through the default tooltip
            if (itemView.ItemMasterElement == null) return string.Empty;
            return Localize.GetContent(ContentLocalizationKeys.ItemName(itemView.ItemMasterElement.ItemGuid));
        }

        private void RefreshDefaultToolTip()
        {
            if (!_usesDefaultToolTip || ItemViewData == null || ItemViewData.IsEmpty) return;
            SetItem(ItemViewData, Count);
        }

        public static async UniTask LoadItemSlotViewPrefab()
        {
            const string itemSlotViewPath = "Vanilla/UI/ItemSlotView";
            var prefab = await AddressableLoader.LoadAsyncDefault<GameObject>(itemSlotViewPath);
            Prefab = prefab.GetComponent<ItemSlotView>();
        }
    }
}
