using System.Collections.Generic;
/// <summary>
/// まだChatGPT実装なので今後修正予定
/// </summary>
public class RailNode
{
    public string Name { get; set; }

    public RailNode(string name)
    {
        Name = name;
    }
}

public class RailGraph
{
    public List<(RailNode from, RailNode to, int distance)> Edges { get; set; }

    public RailGraph()
    {
        Edges = new List<(RailNode, RailNode, int)>();
    }

    public List<(RailNode node, int distance)> CalculateShortestPath(RailNode start, RailNode destination)
    {
        // ダイクストラ法の簡易実装（省略）
        return new List<(RailNode, int)>();
    }

    public List<RailNode> GetAllReachableNodes(RailNode start)
    {
        // 到達可能なノードを取得（省略）
        return new List<RailNode>();
    }
}