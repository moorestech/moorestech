using Client.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// GameUi のルートで入力を先取りする規則。フォーカス同期・ESC・ナビゲーション打ち消し・入力欄と印付き以外のフォーカス拒否を TrickleDown で行う
    /// Root-level input rules for GameUi: focus sync, ESC, navigation cancelling and refusing focus outside text fields and marked elements, all via TrickleDown
    /// </summary>
    /// <remarks>
    /// 拡張点: 全画面に共通するキー・フォーカスの規則はここの Attach に登録を足す。画面固有のキー処理（←→・Enter 等）は各画面の Presenter が持つ
    /// Extension: add panel-wide key/focus rules as registrations in Attach; screen-specific keys (arrows, Enter, ...) belong to each screen's presenter
    /// </remarks>
    public sealed class GameUiInputRules
    {
        // 直前に拒んだ要素。同じ要素が続けてフォーカスを取る間の警告を 1 回にする
        // The element refused last; keeps the warning to one while the same element keeps taking focus
        private static VisualElement _lastRefusedFocus;

        public static void Attach(VisualElement root)
        {
            // 子孫の既定処理より先に走らせるため、すべて TrickleDown で付ける
            // Register everything as TrickleDown so it runs before the descendants' own handling
            root.RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusOutEvent>(OnFocusOut, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            // Tab・矢印・Space・Enter・ESC 由来の選択移動と決定・取消は GameUi では使わない
            // GameUi never uses navigation move/submit/cancel coming from Tab, arrows, Space, Enter or ESC
            root.RegisterCallback<NavigationMoveEvent>(CancelNavigation, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(CancelNavigation, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationCancelEvent>(CancelNavigation, TrickleDown.TrickleDown);
        }

        private static void OnFocusIn(FocusInEvent evt)
        {
            var element = evt.target as VisualElement;
            if (IsInsideTextField(element))
            {
                // フォーカスごとパネルから外れると外れの知らせが根へ届かないので、外れる要素自身の取り外しを購読する
                // Removal with focus never tells the root about the blur, so subscribe to the focused element's own detach
                UiInputExclusivity.SetGameUiTextInputFocused(true);
                element.RegisterCallback<DetachFromPanelEvent>(OnFocusedTextFieldDetached);
                EndRefusalRun();
                return;
            }

            // 印付き（キーで操作するボタン）はフォーカスを保つ。入力欄ではないので入力中の旗は立てない
            // Marked elements (key-operated buttons) keep focus; they are not text fields, so the text-input flag stays down
            if (MayKeepFocus(element))
            {
                EndRefusalRun();
                return;
            }

            // 入力欄と印付き以外はフォーカスを持たせない。focusable=false の付け忘れに開発者が気づけるよう名前を出す
            // Only text fields and marked elements may hold focus; log the element so developers notice a missing focusable=false
            // silent-ok: 同じ要素が続けて取った分は直前と同じ警告になる / 最初の 1 回の LogWarning で開発者が気づく
            // silent-ok: the same element taking focus again repeats the previous warning / developers notice from the first LogWarning
            if (element != _lastRefusedFocus) Debug.LogWarning($"[GameUiInput] non text-field, unmarked element took focus and was blurred: {DescribeElement(element)}");
            _lastRefusedFocus = element;

            // フォーカス切替の配送中に外すと切替処理と競合するので、次の機会に外す
            // Blurring inside the focus-change dispatch fights the switch itself, so blur at the next opportunity
            var root = (VisualElement)evt.currentTarget;
            root.schedule.Execute(() => BlurNonTextFieldFocus(root));

            #region Internal

            // 保ってよいフォーカスで拒否の続きは途切れる。表示されなくなったフォーカスは UITK 自身が外すので見張らない
            // An allowed focus ends the run of refusals; UITK itself blurs a focus that stops being displayed, so nothing watches it here
            void EndRefusalRun()
            {
                _lastRefusedFocus = null;
            }

            #endregion
        }

        private static void OnFocusOut(FocusOutEvent evt)
        {
            // 外れた要素の取り外しの購読は先に解く
            // Drop the detach subscription of the element losing focus first, even when the flag stays up
            var leaving = (VisualElement)evt.target;
            leaving.UnregisterCallback<DetachFromPanelEvent>(OnFocusedTextFieldDetached);

            // 入力欄から入力欄へ移るときは true のまま保つ
            // Keep the flag up when focus moves from one text field to another
            if (IsInsideTextField(evt.relatedTarget as VisualElement)) return;
            UiInputExclusivity.SetGameUiTextInputFocused(false);
        }

        private static void OnFocusedTextFieldDetached(DetachFromPanelEvent evt)
        {
            // 外れた入力欄はもう打てないので、根を通らないフォーカスの外れの代わりに旗を下ろす
            // The removed field can no longer be typed into, so lower the flag in place of the blur that bypasses the root
            ((VisualElement)evt.currentTarget).UnregisterCallback<DetachFromPanelEvent>(OnFocusedTextFieldDetached);
            UiInputExclusivity.SetGameUiTextInputFocused(false);
        }

        private static void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape) return;
            var root = (VisualElement)evt.currentTarget;
            var focused = root.focusController.focusedElement as VisualElement;
            if (!IsInsideTextField(focused)) return;

            // silent-ok: 変換中の ESC は OS が変換を取り消すのでフォーカスも値も触らない / ラッチの Debug.Log が ESC の消費を記録する
            // silent-ok: during IME composition the OS cancels the conversion, so leave focus and value alone / the latch's Debug.Log records the consumed ESC
            if (!ConsumeEscapeOutsideComposition()) return;

            // TextField の既定の ESC（値の巻き戻しと再フォーカス）を止め、値を保ったままフォーカスだけ外す
            // Stop TextField's default ESC (revert value and refocus) and drop only the focus, keeping the value
            evt.StopImmediatePropagation();
            root.focusController.IgnoreEvent(evt);
            UiInputExclusivity.MarkEscapeBlurredTextFieldThisFrame();
            focused.Blur();
        }

        // 入力欄が ESC を使ったことをワールド側（ポーズ・閉じる）へ知らせ、変換中でないか返す
        // Tell the world side (pause / close) that a text field used this ESC, and return whether no IME composition is in progress
        private static bool ConsumeEscapeOutsideComposition()
        {
            UiInputExclusivity.ConsumeEscapeThisFrame();
            return string.IsNullOrEmpty(UnityEngine.Input.compositionString);
        }

        private static void CancelNavigation(EventBase evt)
        {
            evt.StopImmediatePropagation();
            var root = (VisualElement)evt.currentTarget;
            root.focusController.IgnoreEvent(evt);
        }

        private static void BlurNonTextFieldFocus(VisualElement root)
        {
            // silent-ok: 外す前にフォーカスが入力欄・印付き・無しへ移っていれば外すものが無い / 入力欄以外へのフォーカスは OnFocusIn の LogWarning で既に記録済み
            // silent-ok: if focus already moved to a text field, a marked element or nowhere there is nothing to blur / the non-text-field focus was already logged by OnFocusIn
            if (root.focusController?.focusedElement is not VisualElement focused || MayKeepFocus(focused)) return;
            focused.Blur();
        }

        // 入力欄と印付きだけがフォーカスを保ってよい
        // Only text fields and marked elements may keep focus
        private static bool MayKeepFocus(VisualElement element)
        {
            return IsInsideTextField(element) || UiKeyFocusKeepMark.IsMarked(element);
        }

        // TextField のフォーカスは内部の入力要素に来るので祖先まで辿る
        // TextField focus lands on its inner input element, so walk up the ancestors
        private static bool IsInsideTextField(VisualElement element)
        {
            for (var current = element; current != null; current = current.hierarchy.parent)
            {
                if (current is TextField) return true;
            }
            return false;
        }

        private static string DescribeElement(VisualElement element)
        {
            return element == null ? "(null)" : $"{element.GetType().Name} '{element.name}'";
        }
    }
}
