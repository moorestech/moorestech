using System;
using Client.Input;
using UnityEngine;
using UniRx;

namespace Client.Game.InGame.UI.UIState
{
    public class UIRoot : MonoBehaviour
    {
        private readonly ReactiveProperty<bool> _isActive = new(true);

        public IObservable<bool> OnVisibilityChanged => _isActive;
        public bool IsVisible() => _isActive.Value;

        private void Update()
        {
            // TODO InputManagerに移動
            if (HybridInput.GetKey(KeyCode.LeftControl) && HybridInput.GetKeyDown(KeyCode.U))
            {
                _isActive.Value = !_isActive.Value;
            }
        }
    }
}
