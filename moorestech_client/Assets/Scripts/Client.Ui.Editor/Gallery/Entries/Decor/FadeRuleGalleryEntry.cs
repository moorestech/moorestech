using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Editor.Gallery.Entries.Panels;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Decor
{
    /// <summary>
    /// FadeRule の見本。hud の面の上に 88px・250px・幅いっぱいの 3 本を並べ、両端のフェードを見比べる
    /// FadeRule samples: three rules (88px, 250px, full width) on a hud face to compare the end fades
    /// </summary>
    public sealed class FadeRuleGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Decor;
        public string ComponentName => "FadeRule";

        public VisualElement CreateSample()
        {
            var row = PanelsAndDecorSampleSheet.CreateSampleRow();
            var host = new GamePanelSurface("gallery-fade-rule-host", GamePanelSurfaceKind.Hud);
            host.Add(SizedRule("gallery-samples__rule-short"));
            host.Add(SizedRule("gallery-samples__rule-medium"));
            host.Add(new FadeRule());
            row.Add(PanelsAndDecorSampleSheet.Cell(host, "gallery-samples__rule-host"));
            return row;

            #region Internal

            VisualElement SizedRule(string sizeClassName)
            {
                var holder = new VisualElement();
                holder.AddToClassList(sizeClassName);
                holder.Add(new FadeRule());
                return holder;
            }

            #endregion
        }
    }
}
