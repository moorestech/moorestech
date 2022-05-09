namespace MainGame.UnityView.UI.Inventory.View.SubInventory.Element
{
    public interface ISubInventoryElement
    {
        public SubInventoryElementType ElementType { get; }
        public int Priority { get; }
    }
}