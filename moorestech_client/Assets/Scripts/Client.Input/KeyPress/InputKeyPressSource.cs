using System;
using UniRx;

namespace Client.Input.KeyPress
{
    /// <summary>
    /// IUiKeyPressSource の唯一の実装。InputKey の押下（抑止を通った後の OnGetKeyDown・GetKeyDown）をそのまま渡し、自分では判定しない
    /// The only IUiKeyPressSource implementation; it relays the InputKey press (OnGetKeyDown and GetKeyDown after suppression) and judges nothing itself
    /// </summary>
    public abstract class InputKeyPressSource : IUiKeyPressSource
    {
        private readonly InputKey _key;

        public IObservable<Unit> OnPressed { get; }

        protected InputKeyPressSource(InputKey key)
        {
            _key = key;
            OnPressed = Observable.FromEvent(handler => key.OnGetKeyDown += handler, handler => key.OnGetKeyDown -= handler);
        }

        public bool WasPressedThisFrame()
        {
            return _key.GetKeyDown;
        }
    }
}
