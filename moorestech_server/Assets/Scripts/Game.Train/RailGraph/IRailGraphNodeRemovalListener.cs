namespace Game.Train.RailGraph
{
    // レールノード削除通知リスナー
    // Rail node removal listener
    public interface IRailGraphNodeRemovalListener
    {
        void NotifyNodeRemoval(IRailNode removedNode);
    }
}
