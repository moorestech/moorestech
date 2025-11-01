namespace Game.Train.RailGraph
{
    /// <summary>
    /// レール接続情報を表す構造体
    /// Struct representing rail connection information
    /// </summary>
    public struct RailConnectionInfo
    {
        public RailNode FromNode { get; }
        public RailNode ToNode { get; }
        public int Distance { get; }
        
        public RailConnectionInfo(RailNode fromNode, RailNode toNode, int distance)
        {
            FromNode = fromNode;
            ToNode = toNode;
            Distance = distance;
        }
    }

}