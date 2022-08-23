namespace MainGame.UnityView.UI.Builder.BluePrint
{
    public class OneSlot : ISubInventoryElement
    {
        public SubInventoryElementType ElementType => SubInventoryElementType.OneSlot;
        public int Priority { get; }

        //表示するX座標
        public readonly float X;
        //表示するY座標
        public readonly float Y;
        //表示する順番

        public readonly InventorySlotElementOptions Options;

        public OneSlot(float x, float y, int priority, InventorySlotElementOptions options)
        {
            X = x;
            Y = y;
            Priority = priority;
            Options = options;
        }
    }
}