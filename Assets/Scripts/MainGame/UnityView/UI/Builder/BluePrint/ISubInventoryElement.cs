namespace MainGame.UnityView.UI.Builder.BluePrint
{
    public interface ISubInventoryElement
    {
        public UIBluePrintType ElementType { get; }
        public int Priority { get; }
    }
}