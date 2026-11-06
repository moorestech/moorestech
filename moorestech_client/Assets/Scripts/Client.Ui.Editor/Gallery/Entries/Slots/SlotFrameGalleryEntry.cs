using Client.Ui.Components.Slots;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Slots
{
    /// <summary>
    /// SlotFrame の見本。既定とホットバーの行を並べる
    /// SlotFrame samples: default and hotbar rows
    /// </summary>
    public sealed class SlotFrameGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Slots;
        public string ComponentName => "SlotFrame";

        public VisualElement CreateSample()
        {
            var root = SlotGallerySampleSheet.CreateSampleRoot();

            // 既定（持ち物・レシピ）
            // Default (inventory, recipe)
            var plain = SlotGallerySampleSheet.AddRow(root, "slot-gallery__plain");
            AddFrame(plain, new SlotFrameState(false, false, false, false, false), false);
            AddFrame(plain, new SlotFrameState(false, true, false, false, false), true);
            AddFrame(plain, new SlotFrameState(false, false, true, false, false), true);
            AddFrame(plain, new SlotFrameState(true, false, false, false, false), false);
            AddFrame(plain, new SlotFrameState(true, true, false, false, false), true);
            AddFrame(plain, new SlotFrameState(false, true, false, true, false), true);
            AddFrame(plain, new SlotFrameState(false, false, false, false, true), true);

            // ホットバー: 空・選択の空・選択の白面
            // Hotbar: empty, selected empty and selected white
            var hotbar = SlotGallerySampleSheet.AddRow(root, "slot-gallery__hotbar");
            AddFrame(hotbar, new SlotFrameState(false, false, false, false, false), false);
            AddFrame(hotbar, new SlotFrameState(true, false, false, false, false), false);
            AddFrame(hotbar, new SlotFrameState(true, true, false, false, false), true);
            return root;

            #region Internal

            void AddFrame(VisualElement row, SlotFrameState state, bool withIcon)
            {
                var frame = new SlotFrame($"gallery-slot-frame-{row.childCount}");
                frame.AddToClassList("slot-gallery__cell");
                frame.SetState(state);
                if (withIcon)
                {
                    var content = new SlotContent();
                    content.SetIcon(SlotGallerySampleSheet.Icon("1", "Iron Plate"), GameIconFallback.IdText);
                    frame.Add(content);
                }
                row.Add(frame);
            }

            #endregion
        }
    }
}
