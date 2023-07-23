namespace MainGame.UnityView.UI.Builder.Element
{
    public class UIBluePrintProgressArrow : IUIBluePrintElement
    {
        public UIBluePrintElementType ElementElementType => UIBluePrintElementType.ProgressArrow;
        public int Priority { get; }
        public string IdName { get; }
        
        
        public readonly float X;
        public readonly float Y;
        
        public UIBluePrintProgressArrow (float x, float y,int priority, string idName)
        {
            Priority = priority;
            IdName = idName;
            X = x;
            Y = y;
        }
    }
}