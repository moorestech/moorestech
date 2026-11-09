using Client.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.Control
{
    public static class UiPointerHitTest
    {
        public static bool IsPointerOverAnyUi()
        {
            // カーソルロック中はOSカーソルが存在せず、UI上には乗り得ない
            // A locked cursor has no OS pointer and therefore cannot be over UI
            if (InputManager.IsCursorLocked) return false;

            // EventSystem未生成のフレームでもUI判定は要求される。uGUI側は「乗っていない」として扱う
            // The over-UI check is asked for even on frames with no EventSystem; treat the uGUI side as not hovered
            var isOverUguiPanel = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // 選ばれた系だけを読む
            // Read only the chosen system
            var isOverActiveUi = UiInputExclusivity.IsPointerOverActiveUi(HybridInput.GetMousePosition());

            return isOverActiveUi || isOverUguiPanel;
        }
    }
}
