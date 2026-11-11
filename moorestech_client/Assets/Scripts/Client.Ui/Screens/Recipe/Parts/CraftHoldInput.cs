using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// クラフトボタンの押下・離し・外れ・取消と Enter を長押しへ渡す（Web の onPointerDown/Up/Leave/Cancel・onKeyDown/Up・onBlur の写し）
    /// Forwards press, release, leave, cancel and Enter on the craft button to the hold (port of the Web pointer, key and blur handlers)
    /// </summary>
    public sealed class CraftHoldInput
    {
        private const int PrimaryButton = 0;
        private readonly VisualElement _button;
        private readonly CraftHoldDriver _driver;
        private bool _pressed;
        private bool _enterHeld;

        public CraftHoldInput(VisualElement button, CraftHoldDriver driver)
        {
            _button = button;
            _driver = driver;

            // Button の Clickable より先に受けるため TrickleDown で登録する
            // Register in TrickleDown so we see the events before the Button's Clickable
            button.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(_ => Release(), TrickleDown.TrickleDown);
            button.RegisterCallback<PointerLeaveEvent>(_ => Release());
            button.RegisterCallback<PointerCancelEvent>(_ => Release(), TrickleDown.TrickleDown);
            button.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);

            // フォーカス中の Enter は押している間だけ長押し、Space は何もしない（Web onKeyDown/onKeyUp・2026-10-01 裁定）
            // Enter while focused holds only while pressed; Space does nothing (Web onKeyDown/onKeyUp, 2026-10-01 ruling)
            button.RegisterCallback<KeyDownEvent>(OnKeyDown);
            button.RegisterCallback<KeyUpEvent>(OnKeyUp);
            button.RegisterCallback<BlurEvent>(_ => ReleaseEnter());

            // 並べ直しで木から外れるとフォーカスが失われ KeyUp も届かないので、Enter の保持だけ解く（ポインタの保持は付け直しで続く。D-34）
            // Detaching on relayout drops focus and no KeyUp arrives, so release only the Enter hold (a pointer hold resumes on re-attach; D-34)
            button.RegisterCallback<DetachFromPanelEvent>(_ => OnDetached());
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            // silent-ok: Enter 以外のキーは長押しにしない / CraftHoldKeyTest の Space の件が見る
            if (!IsEnter(evt.keyCode)) return;

            // リピートの二重開始は driver が弾く（無効の間に KeyUp を取りこぼして旗が残っても、次の Enter で始め直せる。Web と同じ）
            // The driver rejects repeat restarts (even if a KeyUp was lost while disabled, the next Enter starts again, as on the Web)
            _enterHeld = true;
            _driver.Start();
        }

        private void OnKeyUp(KeyUpEvent evt)
        {
            // silent-ok: Enter 以外の離しは長押しに関係しない / CraftHoldKeyTest の Space の件が見る
            if (!IsEnter(evt.keyCode)) return;
            ReleaseEnter();
        }

        private void OnDetached()
        {
            // silent-ok: Enter で保持していなければ外れても止めない（ポインタの保持は付け直しで続く）/ D-34 と RecipeViewerViewTest の並べ直しの件が見る
            if (!_enterHeld) return;
            ReleaseEnter();
        }

        private void ReleaseEnter()
        {
            _enterHeld = false;
            _driver.Stop();
        }

        private static bool IsEnter(KeyCode keyCode)
        {
            return keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter;
        }

        private void OnDown(PointerDownEvent evt)
        {
            // silent-ok: 左以外は Web と同じく長押しにしない / CraftHoldTest の左押下と録画が左を見る
            if (evt.button != PrimaryButton) return;
            _pressed = true;
            _driver.Start();
        }

        private void OnMove(PointerMoveEvent evt)
        {
            // Clickable が捕捉していると Leave が来ないので、外へ出たかを位置で読む（Web の pointerleave と同じ時点）
            // While Clickable holds capture no Leave arrives, so read the position to detect leaving (same moment as the Web pointerleave)
            if (_pressed && !_button.worldBound.Contains(evt.position)) Release();

            // 他ボタンを押したままの左解放は PointerUp でなく chorded な PointerMove で届く（UiPressGesture と同じ判定）
            // A primary release while another button is held arrives as a chorded PointerMove, not PointerUp (same check as UiPressGesture)
            if (_pressed && evt.button == PrimaryButton && (evt.pressedButtons & 1) == 0) Release();
        }

        private void Release()
        {
            _pressed = false;
            _driver.Stop();
        }
    }
}
