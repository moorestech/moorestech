namespace MainGame.UnityView.UI.Builder.BluePrint
{
    public class UIBluePrintProgressArrow : IUIBluePrintElement
    {
        public UIBluePrintType ElementType => UIBluePrintType.ProgressArrow;
        public int Priority { get; }
        public string IdName { get; }
        
        
        public UIBluePrintProgressArrow(int priority, string idName)
        {
            Priority = priority;
            IdName = idName;
        }
    }
}