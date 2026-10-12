using Client.Game.InGame.Control;
using Client.Input;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    /// <summary>
    ///     UI外の押下と解放を1クリックとして扱う
    ///     Treats a press and release outside UI as one click
    /// </summary>
    public class BlueprintCopyClickInput
    {
        private bool _isPressRegistered;

        public bool TryConsumeClick()
        {
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _isPressRegistered = true;
            if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return false;

            var isClick = _isPressRegistered && !UiPointerHitTest.IsPointerOverAnyUi();
            _isPressRegistered = false;
            return isClick;
        }

        public void Reset()
        {
            _isPressRegistered = false;
        }
    }
}
