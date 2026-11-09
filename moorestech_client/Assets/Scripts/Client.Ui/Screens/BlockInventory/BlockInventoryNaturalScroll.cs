using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 自然高の面の本文の器（Web .naturalBodyScroll）。外へ負マージン・中へ同じ幅の逃げを当て、本文の位置と面の大きさを動かさない
    /// Body holder of the natural-height panel (Web .naturalBodyScroll); a negative outer margin and an equal inner clearance keep the body position and panel size unchanged
    /// </summary>
    public sealed class BlockInventoryNaturalScroll
    {
        public const string ClassName = "block-inventory__natural-scroll";
        private const string BodyBleedProperty = "--block-inventory-body-bleed";

        public GameScrollView Root { get; private set; }

        public BlockInventoryNaturalScroll()
        {
            Root = new GameScrollView("block-inventory-natural-scroll");
            Root.AddToClassList(ClassName);

            // USS に calc() が無いので、受け直した逃げの幅を C# で負号付きのマージンと正の内側余白に当てる
            // USS has no calc(), so C# applies the re-declared bleed as a negative margin and a positive inner padding
            Root.RegisterCallback<CustomStyleResolvedEvent>(_ => ApplyBleed());
        }

        public void Wrap(VisualElement body)
        {
            Root.Clear();
            Root.Add(body);
        }

        public void Unwrap()
        {
            Root.Clear();
            Root.RemoveFromHierarchy();
        }

        private void ApplyBleed()
        {
            // silent-ok: 読めなければ逃げ無しのまま / UssCustomStyleReader が LogError で知らせる
            if (!UssCustomStyleReader.TryReadPixels(Root.customStyle, BodyBleedProperty, out var bleed)) return;
            Root.style.marginTop = -bleed;
            Root.style.marginRight = -bleed;
            Root.style.marginBottom = -bleed;
            Root.style.marginLeft = -bleed;
            Root.contentContainer.style.paddingTop = bleed;
            Root.contentContainer.style.paddingRight = bleed;
            Root.contentContainer.style.paddingBottom = bleed;
            Root.contentContainer.style.paddingLeft = bleed;
        }
    }
}
