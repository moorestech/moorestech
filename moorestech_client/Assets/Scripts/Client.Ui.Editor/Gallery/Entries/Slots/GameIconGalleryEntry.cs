using Client.Ui.Components.Slots;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Slots
{
    /// <summary>
    /// GameIcon の見本。絵・#id・名前・何も出さない、の 4 つを 48px の箱に並べる
    /// GameIcon samples: picture, #id, name and nothing, in 48px boxes
    /// </summary>
    public sealed class GameIconGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Slots;
        public string ComponentName => "GameIcon";

        public VisualElement CreateSample()
        {
            var root = SlotGallerySampleSheet.CreateSampleRoot();
            var row = SlotGallerySampleSheet.AddRow(root, "slot-gallery__plain");
            AddIcon(row).SetIcon(SlotGallerySampleSheet.Icon("12", "Iron Plate"), GameIconFallback.IdText);
            AddIcon(row).SetIcon(SlotGallerySampleSheet.MissingIcon("123456789", "Iron Plate"), GameIconFallback.IdText);
            AddIcon(row).SetIcon(SlotGallerySampleSheet.MissingIcon("7", "Water"), GameIconFallback.Label);
            AddIcon(row).SetIcon(SlotGallerySampleSheet.MissingIcon("7", "Water"), GameIconFallback.None);
            return root;

            #region Internal

            GameIcon AddIcon(VisualElement parent)
            {
                var icon = new GameIcon();
                icon.AddToClassList("slot-gallery__cell");
                icon.style.width = 48;
                icon.style.height = 48;
                parent.Add(icon);
                return icon;
            }

            #endregion
        }
    }
}
