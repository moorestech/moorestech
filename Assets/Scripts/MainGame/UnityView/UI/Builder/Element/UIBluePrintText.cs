namespace MainGame.UnityView.UI.Builder.Element
{
    public class UIBluePrintText : IUIBluePrintElement
    {
        public UIBluePrintElementType ElementElementType => UIBluePrintElementType.Text;
        public int Priority { get; }
        public string IdName { get; }

        public readonly float X;
        public readonly float Y;
        public readonly string DefaultText;
        public readonly int FontSize;
        
        public UIBluePrintText(float x, float y,int priority, string defaultText, int fontSize, string idName = "")
        {
            Priority = priority;
            X = x;
            Y = y;
            DefaultText = defaultText;
            FontSize = fontSize;
            IdName = idName;
        }
    }
}