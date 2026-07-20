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

            return EventSystem.current.IsPointerOverGameObject() || WebUiInputExclusivity.IsPointerOverWebUi;
        }
    }
}
