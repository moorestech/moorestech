using System;
using UniRx;

namespace Client.Input.KeyPress
{
    /// <summary>
    /// キーの口（ISkitAdvanceKeyPressSource・ICloseUiKeyPressSource）の唯一の実装の基底。InputKey の押下（抑止を通った後の OnGetKeyDown・GetKeyDown）をそのまま渡し、自分では判定しない
    /// The single implementation base of the key ports (ISkitAdvanceKeyPressSource, ICloseUiKeyPressSource); it relays the InputKey press (OnGetKeyDown and GetKeyDown after suppression) and judges nothing itself
    /// </summary>
    public abstract class InputKeyPressSource
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
