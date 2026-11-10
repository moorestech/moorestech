using System;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 主要アクションの青グラデボタン
    /// Primary action button with the blue gradient
    /// </summary>
    /// <remarks>
    /// 見た目の組は変種で選び、研究の組は部品 USS の修飾クラスが持つ（A-190）。画面が祖先で上書きしてよいのは研究の寸法だけ。長押しは置く側の画面の部品が持つ（A-166）。
    /// The variant picks the look set and the research set lives in the component USS modifier (A-190); screens may only override research size on an ancestor. Hold-to-craft belongs to the placing screen's own part (A-166).
    /// </remarks>
    public sealed class RecipeActionButton : Button
    {
        public const string UssClassName = "recipe-action-button";
        public const string ResearchUssClassName = "recipe-action-button--research";
        public const string LabelUssClassName = "recipe-action-button__label";
        public const string FaceStartProperty = "--recipe-action-button-face-start";
        public const string FaceMiddleProperty = "--recipe-action-button-face-middle";
        public const string FaceEndProperty = "--recipe-action-button-face-end";
        public const string FaceBorderWidthProperty = "--recipe-action-button-face-border-width";

        private readonly Subject<Unit> _clicked = new();
        private readonly RecipeActionLabelShadow _labelShadow;
        private readonly LocalizedLabel _label;

        public IObservable<Unit> OnClicked => _clicked;

        public RecipeActionButton(string testid, UiText label, RecipeActionButtonVariant variant)
        {
            name = testid;
            AddToClassList(UssClassName);

            // GameUi は入力欄以外にフォーカスを持たせない
            // GameUi never lets anything but text fields hold focus
            focusable = false;

            // 面・文言の影・文言の順に重ねる（どれも押下を横取りしない）
            // Stack face, label shadow, then label (none steals the press)
            Add(new RecipeActionButtonFace());
            _labelShadow = new RecipeActionLabelShadow();
            Add(_labelShadow);
            _label = new LocalizedLabel { pickingMode = PickingMode.Ignore };
            _label.AddToClassList(LabelUssClassName);
            // クラフトは 500 の字形を文言と影の字に渡し、研究は修飾クラスで研究の組（400 の既定の字・影なし）に切り替える
            // Craft gives the 500 face to the label and shadow copies; research switches to its set (default 400 face, no shadow) via the modifier
            if (variant == RecipeActionButtonVariant.Research)
            {
                AddToClassList(ResearchUssClassName);
            }
            else
            {
                _label.AddToClassList(UiTypographyClass.TextMedium);
                _labelShadow.AddGlyphClass(UiTypographyClass.TextMedium);
            }
            SetLabel(label);
            Add(_label);

            // 押下は UniRx で外へ出す
            // Presses leave through UniRx
            clicked += () => _clicked.OnNext(Unit.Default);
        }

        public void SetLabel(UiText label)
        {
            _labelShadow.SetText(label);
            _label.SetText(label);
        }
    }
}
