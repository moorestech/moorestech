namespace MainGame.UnityView.UI.Inventory.View.SubInventory.Element
{
    public class TextElement : ISubInventoryElement
    {
        public SubInventoryElementType ElementType => SubInventoryElementType.Text;
        public int Priority { get; }
        
        public readonly float X;
        public readonly float Y;
        public readonly string DefaultText;
        public readonly int FontSize;
        
        public TextElement( float x, float y,int priority, string defaultText, int fontSize)
        {
            Priority = priority;
            X = x;
            Y = y;
            DefaultText = defaultText;
            FontSize = fontSize;
        }
    }
}