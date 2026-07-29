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
    public class FluidSlotView : MonoBehaviour
    {
        public static FluidSlotView Prefab { get; private set; }
        
        public IObservable<(FluidSlotView, ItemUIEventType)> OnPointerEvent => commonSlotView.OnPointerEvent.Select(e => (this, e.Item2));
        
        [SerializeField] private CommonSlotView commonSlotView;
        
        
        public void SetFluid(FluidViewData fluidView, double amount, string toolTipText = null)
        {
            if (fluidView == null || fluidView.FluidId == FluidMaster.EmptyFluidId)
            {
                commonSlotView.SetViewClear();
            }
            else
            {
                if (string.IsNullOrEmpty(toolTipText))
                {
                    toolTipText = GetToolTipText(fluidView);
                }
                
                var countText = amount != 0 ? amount.ToString("N0") : string.Empty;
                commonSlotView.SetView(fluidView.FluidImage, countText, toolTipText);
            }
        }
        
        public void SetSlotViewOption(CommonSlotViewOption slotOption)
        {
            commonSlotView.SetSlotViewOption(slotOption);
        }
        
        public void SetActive(bool active)
        {
            commonSlotView.SetActive(active);
        }
        
        
        public static string GetToolTipText(FluidViewData fluidView)
        {
            return $"{fluidView.FluidName}";
        }
        
        public static async UniTask LoadItemSlotViewPrefab()
        {
            const string itemSlotViewPath = "Vanilla/UI/FluidSlotView";
            var prefab = await AddressableLoader.LoadAsyncDefault<GameObject>(itemSlotViewPath);
            Prefab = prefab.GetComponent<FluidSlotView>();
        }
    }
}
