using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// GamePanel の題。Web の transform: translateX(--title-shift-x) scale(--title-scale-x, --title-scale-y) をつまみから当てる
    /// GamePanel title; applies the Web transform translateX(--title-shift-x) scale(--title-scale-x, --title-scale-y) from the knobs
    /// </summary>
    public sealed class GamePanelTitle : LocalizedLabel
    {
        public GamePanelTitle()
        {
            AddToClassList("game-panel__title");
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            // silent-ok: 読めないつまみがあれば前回の transform のまま / UssCustomStyleReader が読めなかった名前を LogError で出す
            if (!UssCustomStyleReader.TryReadPixels(customStyle, "--game-panel-title-shift-x", out var shiftX)) return;
            if (!UssCustomStyleReader.TryReadNumber(customStyle, "--game-panel-title-scale-x", out var scaleX)) return;
            if (!UssCustomStyleReader.TryReadNumber(customStyle, "--game-panel-title-scale-y", out var scaleY)) return;

            // 原点は USS の transform-origin: left center
            // The origin is the USS transform-origin: left center
            style.translate = new Translate(new Length(shiftX), new Length(0f));
            style.scale = new Scale(new Vector2(scaleX, scaleY));
        }
    }
}
