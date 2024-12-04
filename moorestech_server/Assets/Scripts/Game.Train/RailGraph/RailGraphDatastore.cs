using System.Collections.Generic;
namespace Game.Train.RailGraph
{
    ///
    /// NodeId is a unique ID to identify the node
    ///
    public class RailGraphDatastore
    {
        private readonly Dictionary<int, RailNode> _nodes = new();

        public RailGraphDatastore()
        {
            //UnityEngine.Debug.Log("RailGraphDatastore created");
        }

        
        public void AddNode(RailNode node)
        {
            _nodes[node.NodeId] = node;
        }

        public void RemoveNode(int nodeId)
        {
            _nodes.Remove(nodeId);
        }
        
    }
}
