using System;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// GameUi 共通の入力欄。全選択の抑止・IME の未確定文字列の下線・候補窓のキャレット追従を持つ TextField の包み
    /// Shared GameUi text field: wraps a TextField with no select-all, an IME composition underline and caret-following candidate window
    /// </summary>
    /// <remarks>
    /// 拡張点: 入力欄に共通する見た目・IME の振る舞いはここへ足す。文字数の検証や絞り込みなど画面固有の処理は OnValueChanged の購読側が持つ。ESC・フォーカス同期はルートの GameUiInputRules が受け持つ
    /// Extension: add text-field-wide visuals and IME behaviour here; screen-specific logic (length checks, filtering) belongs to OnValueChanged subscribers, and ESC / focus sync belong to the root GameUiInputRules
    /// </remarks>
    public sealed class GameTextField : VisualElement
    {
        public const string UssClassName = "game-text-field";

        private readonly Subject<string> _valueChanged = new();
        private readonly TextElement _textElement;
        private readonly ImeCompositionUnderline _underline;
        private readonly IVisualElementScheduledItem _focusedTick;

        public UnityEngine.UIElements.TextField Field { get; }

        /// <summary>
        /// 値の変化。IME の変換中の未確定文字列も値として流れる。購読側（文字数の検証・即時の絞り込み）は未確定の文字列でも壊れないこと（§4.9）
        /// Value changes. Uncommitted IME composition text also flows as a value; subscribers (length checks, live filtering) must not break on it (§4.9)
        /// </summary>
        public IObservable<string> OnValueChanged => _valueChanged;

        public GameTextField(string testid, bool multiline)
        {
            // 外側の箱はフォーカスもポインタも受けず、testid は DSL の SetText が引く内側の TextField に付ける
            // The outer box takes neither focus nor pointer; the testid goes on the inner TextField that the DSL's SetText looks up
            focusable = false;
            pickingMode = PickingMode.Ignore;
            AddToClassList(UssClassName);

            // クリックでのフォーカスで本文を全選択しない（1 打で置き換わるのを防ぐ）
            // Do not select everything on click-focus, so one keystroke cannot replace the whole text
            Field = new UnityEngine.UIElements.TextField { name = testid, multiline = multiline };
            Field.selectAllOnFocus = false;
            Field.selectAllOnMouseUp = false;
            Field.RegisterValueChangedCallback(evt => _valueChanged.OnNext(evt.newValue));
            Add(Field);

            // IME の状態はイベントが無いので、フォーカス中だけパネルのスケジューラで毎フレーム見る
            // IME state has no events, so watch it every frame on the panel scheduler only while focused
            _textElement = Field.Q<TextElement>();
            _underline = new ImeCompositionUnderline(Field, _textElement);
            _focusedTick = Field.schedule.Execute(OnFocusedTick).Every(0);
            _focusedTick.Pause();
            Field.RegisterCallback<FocusInEvent>(_ => _focusedTick.Resume());
            Field.RegisterCallback<FocusOutEvent>(_ => OnFocusLost());
        }

        public void SetValueWithoutNotify(string value)
        {
            Field.SetValueWithoutNotify(value);
        }

        // EditMode では未確定文字列を作れないので、L1 から直接呼べるよう下線の更新を分けている
        // Composition text cannot be produced in EditMode, so the underline update is split out for L1 to call directly
        internal void ApplyComposition(string compositionString)
        {
            _underline.Apply(compositionString);
        }

        private void OnFocusedTick()
        {
            _underline.Refresh(UnityEngine.Input.compositionString);

            // キャレットは行の下端を指すので候補窓は行のすぐ下に出る。UITK 自身が書くパネル座標の値を、スクリーンのピクセルへ換算した値で上書きする
            // The caret points at the line bottom so the window sits just below the line; overwrite UITK's own panel-space value with the screen-pixel conversion
            var caretPanelPoint = _textElement.LocalToWorld(Field.textSelection.cursorPosition);
            var panelSize = panel.visualTree.layout.size;
            var screenSize = new Vector2(Screen.width, Screen.height);
            UnityEngine.Input.compositionCursorPos = ImeCaretScreenPosition.FromPanel(caretPanelPoint, panelSize, screenSize, ImeCaretScreenPosition.PlatformOrigin);
        }

        private void OnFocusLost()
        {
            // フォーカスが外れると変換は終わるので、見張りを止めて下線を消す
            // Losing focus ends the composition, so stop watching and hide the underline
            _focusedTick.Pause();
            _underline.Reset();
        }
    }
}
