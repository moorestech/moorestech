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
        public event Action<InventoryItemSlot> OnRightClickDown;
        public void InvokeOnRightClickDown(InventoryItemSlot slot) { OnRightClickDown?.Invoke(slot); }
        
        /// <summary>
        /// 左クリックされた時に発生するイベント
        /// IsEnableControllerEventがfalseでも発生する
        /// </summary>
        public event Action<InventoryItemSlot> OnLeftClickDown;
        public void InvokeOnLeftClickDown(InventoryItemSlot slot) { OnLeftClickDown?.Invoke(slot); }
    }
}