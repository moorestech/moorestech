namespace MainGame.UnityView.UI.Builder.BluePrint
{
    public interface ISubInventoryElement
    {
        public SubInventoryElementType ElementType { get; }
        public int Priority { get; }
    }
}