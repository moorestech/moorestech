using Client.Ui.Components.Controls;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    /// <summary>
    /// GameScrollView の見本。高さ 160px に 12 行を入れてバーを出す
    /// GameScrollView sample: twelve rows in 160px so the bar shows
    /// </summary>
    public sealed class GameScrollViewGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "GameScrollView";

        public VisualElement CreateSample()
        {
            var host = new VisualElement();
            host.style.width = 240;
            host.style.height = 160;
            var view = new GameScrollView("gallery-scroll");
            for (var i = 0; i < 12; i++) view.Add(new Label($"Row {i + 1}"));
            host.Add(view);
            return host;
        }
    }
}
