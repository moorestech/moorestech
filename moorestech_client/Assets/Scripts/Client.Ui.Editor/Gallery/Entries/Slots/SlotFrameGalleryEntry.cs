using Client.Ui.Components.Slots;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Slots
{
    /// <summary>
    /// SlotFrame の見本。既定の行に状態と組み合わせ、ホットバーの行に inset のリングと選択の白面
    /// SlotFrame samples: states and combinations on the default row, inset rings and the selected white face on the hotbar row
    /// </summary>
    public sealed class SlotFrameGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Slots;
        public string ComponentName => "SlotFrame";

        public VisualElement CreateSample()
        {
            var root = SlotGallerySampleSheet.CreateSampleRoot();

            // 既定（持ち物・レシピ）: 空・白面・灰面・選択・選択の白面・不足・ゴースト
            // Default (inventory, recipe): empty, white, gray, selected, selected white, insufficient and ghost
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
