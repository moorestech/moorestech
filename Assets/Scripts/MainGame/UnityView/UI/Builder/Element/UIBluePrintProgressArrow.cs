namespace MainGame.UnityView.UI.Builder.Element
{
    public class UIBluePrintProgressArrow : IUIBluePrintElement
    {
        public UIBluePrintElementType ElementElementType => UIBluePrintElementType.ProgressArrow;
        public int Priority { get; }
        public string IdName { get; }
        
        
        public UIBluePrintProgressArrow(int priority, string idName)
        {
            Priority = priority;
            IdName = idName;
        }
    }
}