using System;
using Client.Input;
using Client.Input.Pointer;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input.Pointer
{
    /// <summary>
    /// 本番のポインタ。掴み絵とカーソルツールチップが同じ 1 個を読む
    /// Production pointer; the grab picture and the cursor tooltip read the same instance
    /// </summary>
    public sealed class InputSystemUiScreenPointer : IUiScreenPointer
    {
        private readonly InputSystemPointerPositionStream _positions;

        public InputSystemUiScreenPointer(InputSystemPointerPositionStream positions)
        {
            _positions = positions;
        }

        public IObservable<Vector2> OnPointerMovedOnScreen => _positions.OnPointerScreenPositionChanged;

        public Vector2 CurrentPointerOnScreen()
        {
            var mouse = HybridInput.GetMousePosition();
            return new Vector2(mouse.x, mouse.y);
        }

        public Vector2 ScreenToPanel(IPanel panel, Vector2 bottomLeftScreenPosition)
        {
            return UiScreenToPanel.FromBottomLeft(panel, bottomLeftScreenPosition);
        }
    }
}
