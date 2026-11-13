using System;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// IME 対応の共通入力欄（見た目2種・置き文字・輪）
    /// Shared IME-aware text field (two looks, placeholder, ring)
    /// </summary>
    /// <remarks>
    /// 拡張点: 画面固有の処理は購読側、寸法は置く側が持つ
    /// Extension: subscribers own screen logic, callers own sizing
    /// </remarks>
    public sealed class GameTextField : VisualElement
    {
        public const string UssClassName = "game-text-field";
        public const string FocusedUssClassName = "game-text-field--focused";
        public const string MultilineUssClassName = "game-text-field--multiline";
        public const string FocusRingUssClassName = "game-text-field__focus-ring";

        private readonly Subject<string> _valueChanged = new();
        private readonly TextElement _textElement;
        private readonly ImeCompositionUnderline _underline;
        private readonly IVisualElementScheduledItem _focusedTick;
        private LocalizedTextBinding _placeholder;

        public UnityEngine.UIElements.TextField Field { get; }

        /// <summary>
        /// 値の変化。IME の変換中の未確定文字列も値として流れる。購読側（文字数の検証・即時の絞り込み）は未確定の文字列でも壊れないこと（§4.9）
        /// Value changes. Uncommitted IME composition text also flows as a value; subscribers (length checks, live filtering) must not break on it (§4.9)
        /// </summary>
        public IObservable<string> OnValueChanged => _valueChanged;

        public GameTextField(string testid, bool multiline, GameTextFieldLook look)
        {
            // 外側の箱はフォーカスもポインタも受けず、testid は DSL の SetText が引く内側の TextField に付ける
            // The outer box takes neither focus nor pointer; the testid goes on the inner TextField that the DSL's SetText looks up
            focusable = false;
            pickingMode = PickingMode.Ignore;
            AddToClassList(UssClassName);
            AddToClassList(LookUssClassName(look));
            EnableInClassList(MultilineUssClassName, multiline);

            // クリックで全選択しない
            // No select-all on click focus
            Field = new UnityEngine.UIElements.TextField { name = testid, multiline = multiline };
            Field.selectAllOnFocus = false;
            Field.selectAllOnMouseUp = false;
            Field.RegisterValueChangedCallback(evt => _valueChanged.OnNext(evt.newValue));
            Add(Field);

            // Web の :focus-visible の outline を子で描く（出す条件は USS が決める）
            // Draw the Web :focus-visible outline as a child; USS decides when it shows
            var focusRing = new VisualElement { pickingMode = PickingMode.Ignore };
            focusRing.AddToClassList(FocusRingUssClassName);
            Add(focusRing);

            // IME状態は毎フレーム見る（フォーカス中のみ）
            // Watch IME state per frame while focused
            _textElement = Field.Q<TextElement>();
            _underline = new ImeCompositionUnderline(Field, _textElement);
            _focusedTick = Field.schedule.Execute(OnFocusedTick).Every(0);
            _focusedTick.Pause();
            Field.RegisterCallback<FocusInEvent>(_ => OnFocusGained());
            Field.RegisterCallback<FocusOutEvent>(_ => OnFocusLost());

            #region Internal

            static string LookUssClassName(GameTextFieldLook look)
            {
                return look switch
                {
                    GameTextFieldLook.GaugeInset => "game-text-field--gauge-inset",
                    GameTextFieldLook.MantineInput => "game-text-field--mantine-input",
                    _ => throw new ArgumentOutOfRangeException(nameof(look), look, "unknown GameTextFieldLook"),
                };
            }

            #endregion
        }

        public void SetValueWithoutNotify(string value)
        {
            Field.SetValueWithoutNotify(value);
        }

        public void SetPlaceholder(UiText placeholder)
        {
            // 束縛は初めて置き文字を持つときに作る（置き文字の無い欄は文無しの LogError を出さない）
            // Create the binding on first use, so fields without a placeholder never log a missing text
            if (_placeholder == null)
            {
                _placeholder = new LocalizedTextBinding(Field);
                _placeholder.OnResolved.Subscribe(text => Field.textEdition.placeholder = text);
            }
            _placeholder.SetText(placeholder);
        }

        private void OnFocusGained()
        {
            // 輪と枠の色のためにフォーカスをクラスで USS へ渡す
            // Hand focus to USS as a class for the ring and border colour
            AddToClassList(FocusedUssClassName);
            _focusedTick.Resume();
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
            // 外れたら見張り・下線・輪を止める
            // Stop the watcher, underline and ring on focus loss
            RemoveFromClassList(FocusedUssClassName);
            _focusedTick.Pause();
            _underline.Reset();
        }
    }
}
