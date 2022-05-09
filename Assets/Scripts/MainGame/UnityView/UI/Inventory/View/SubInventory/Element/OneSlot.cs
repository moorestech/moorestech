namespace MainGame.UnityView.UI.Inventory.View.SubInventory.Element
{
    public class OneSlot : ISubInventoryElement
    {
        public SubInventoryElementType ElementType => SubInventoryElementType.OneSlot;
        public int Priority => priority;
        
        //表示するX座標
        public float X;
        //表示するY座標
        public float Y;
        //表示する順番
        public int priority;

        public InventorySlotElementOptions Options;

        public OneSlot(float x, float y, int priority, InventorySlotElementOptions options)
        {
            X = x;
            Y = y;
            this.priority = priority;
            Options = options;
        }
    }
}