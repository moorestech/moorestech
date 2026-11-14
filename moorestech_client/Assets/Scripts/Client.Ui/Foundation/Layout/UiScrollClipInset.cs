using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layout
{
    /// <summary>
    /// スクロールの切り抜きの外へ縁取り・影を逃がす唯一の式（Web の margin:-clip-inset と padding:clip-inset。A-245）
    /// The single formula that lets outlines and shadows escape the scroll clip (Web margin:-clip-inset plus padding:clip-inset; A-245)
    /// </summary>
    public static class UiScrollClipInset
    {
        public static void Apply(VisualElement outer, VisualElement inner, float inset)
        {
            ApplyOuterMargin(outer, inset);
            ApplyInnerPadding(inner, inset);
        }

        // 外の要素と内の要素の持ち主が違うとき用（内は部品が自分の口から当てる）
        // For when the outer and inner elements have different owners (the component applies the inner side through its own port)
        public static void ApplyOuterMargin(VisualElement outer, float inset)
        {
            outer.style.marginTop = -inset;
            outer.style.marginRight = -inset;
            outer.style.marginBottom = -inset;
            outer.style.marginLeft = -inset;
        }

        public static void ApplyInnerPadding(VisualElement inner, float inset)
        {
            inner.style.paddingTop = inset;
            inner.style.paddingRight = inset;
            inner.style.paddingBottom = inset;
            inner.style.paddingLeft = inset;
        }

        // 縦は Web が逃がさない画面（研究の計算の帯）用
        // For screens where the Web does not escape vertically (the research calculation band)
        public static void ApplyHorizontal(VisualElement outer, VisualElement inner, float inset)
        {
            outer.style.marginLeft = -inset;
            outer.style.marginRight = -inset;
            inner.style.paddingLeft = inset;
            inner.style.paddingRight = inset;
        }
    }
}
