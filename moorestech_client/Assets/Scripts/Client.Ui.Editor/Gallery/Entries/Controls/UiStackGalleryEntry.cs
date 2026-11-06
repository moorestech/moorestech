using Client.Ui.Foundation.Layout;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    /// <summary>
    /// Stack/Group の gap 段別の見本
    /// Stack/Group sample per gap step
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
