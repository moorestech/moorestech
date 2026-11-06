using Client.Ui.Foundation.Layout;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    /// <summary>
    /// UiStack と UiGroup の見本。gap の段ごとに 3 つの箱を並べる（基盤の並びを置き場として操作部品の族に置く）
    /// UiStack and UiGroup sample: three boxes per gap step (the foundation layout is shown under the controls family)
    /// </summary>
    public sealed class UiStackGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "UiStack・UiGroup";

        public VisualElement CreateSample()
        {
            var root = new UiStack(UiGap.Lg);
            foreach (var gap in new[] { UiGap.Xs, UiGap.Md, UiGap.Xl })
            {
                var group = new UiGroup(gap);
                for (var i = 0; i < 3; i++) group.AddItem(Box());
                root.AddItem(group);
            }
            return root;

            #region Internal

            VisualElement Box()
            {
                // 色を C# に書かないので、箱は文字で見せる
                // Colors stay out of C#, so each box is shown as text
                return new Label("Box");
            }

            #endregion
        }
    }
}
