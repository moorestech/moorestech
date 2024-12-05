using System.Collections.Generic;
namespace Game.Train.RailGraph
{
    ///
    /// NodeId is a unique ID to identify the node
    ///
    public class RailGraphDatastore
    {
        private readonly Dictionary<RailNodeId, RailNode> _nodes = new();

        public RailGraphDatastore()
        {
        }

        
        public void AddNode(RailNode node)
        {
            _nodes[node.NodeId] = node;
        }

        public void RemoveNode(RailNodeId nodeId)
        {
            _nodes.Remove(nodeId);
        }
        
    }
}
