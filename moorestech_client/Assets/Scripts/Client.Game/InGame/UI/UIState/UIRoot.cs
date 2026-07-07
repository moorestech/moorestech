using System;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class UIRoot : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        private bool _isActive = true;

        private void Update()
        {
            // TODO InputManagerに移動
            if (HybridInput.GetKey(KeyCode.LeftControl) && HybridInput.GetKeyDown(KeyCode.U))
            {
                _isActive = !_isActive;
                canvasGroup.alpha = _isActive ? 1 : 0;
            }
        }
    }
}