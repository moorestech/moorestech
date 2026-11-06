using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery
{
    /// <summary>
    /// ギャラリーの中身。族の見出しの下に、部品名と見本を 1 行ずつ並べる
    /// Gallery contents: under each family heading, one row per component with its name and sample
    /// </summary>
    public sealed class UiGalleryPage : VisualElement
    {
        public UiGalleryPage(IReadOnlyList<IUiGalleryEntry> entries)
        {
            AddToClassList("ui-gallery");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            Add(scroll);
            UiGalleryFamily? currentFamily = null;
            foreach (var entry in entries)
            {
                // 族が変わったら見出しを置く
                // Put a heading when the family changes
                if (currentFamily != entry.Family)
                {
                    currentFamily = entry.Family;
                    var heading = new Label(entry.Family.ToString());
                    heading.AddToClassList("ui-gallery__family");
                    scroll.Add(heading);
                }
                var row = new VisualElement();
                row.AddToClassList("ui-gallery__row");
                var name = new Label(entry.ComponentName);
                name.AddToClassList("ui-gallery__name");
                row.Add(name);
                row.Add(entry.CreateSample());
                scroll.Add(row);
            }
        }
    }
}
