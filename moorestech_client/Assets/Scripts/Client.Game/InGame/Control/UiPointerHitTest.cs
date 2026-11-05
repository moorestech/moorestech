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
            if (Cursor.lockState == CursorLockMode.Locked) return false;

            // EventSystem未生成のフレームでもUI判定は要求される。uGUI側は「乗っていない」として扱う
            // The over-UI check is asked for even on frames with no EventSystem; treat the uGUI side as not hovered
            var isOverUguiPanel = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // GameUi(UITK)のPickはprobe未登録ならfalse、WebUiは休止中ならfalse
            // GameUi (UITK) pick is false without a probe, and the Web UI is false while suspended
            var isOverGameUi = UiInputExclusivity.IsPointerOverGameUi(HybridInput.GetMousePosition());

            return isOverGameUi || UiInputExclusivity.IsPointerOverWebUi || isOverUguiPanel;
        }
    }
}
