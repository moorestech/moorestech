namespace Core.BeltTransport
{
    public interface IBeltItemDropObserver
    {
        void OnDropped(BeltCellItemState item, string reason);
    }
}
