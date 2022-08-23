using System;
using MainGame.UnityView.UI.Builder.Unity;

namespace MainGame.UnityView.UI.Builder.BluePrint
{
    public class InventorySlotElementOptions
    {
        /// <summary>
        /// そのスロットでイベントを発生させるかどうか
        /// </summary>
        public bool IsEnableControllerEvent = true;

        /// <summary>
        /// そのスロットのボタンが有効かどうか
        /// </summary>
        public bool IsButtonEnable = true;
        
        
        /// <summary>
        /// 右クリックされた時に発生するイベント
        /// IsEnableControllerEventがfalseでも発生する
        /// </summary>
        public event Action<UIBuilderItemSlotObject> OnRightClickDown;
        public void InvokeOnRightClickDown(UIBuilderItemSlotObject slotObject) { OnRightClickDown?.Invoke(slotObject); }
        
        /// <summary>
        /// 左クリックされた時に発生するイベント
        /// IsEnableControllerEventがfalseでも発生する
        /// </summary>
        public event Action<UIBuilderItemSlotObject> OnLeftClickDown;
        public void InvokeOnLeftClickDown(UIBuilderItemSlotObject slotObject) { OnLeftClickDown?.Invoke(slotObject); }
    }
}