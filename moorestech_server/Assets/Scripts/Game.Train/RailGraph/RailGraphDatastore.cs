using System.Collections.Generic;
///
/// NodeId is a unique ID to identify the node
///
namespace Game.Train.Common
{ 
    public class RailGraphDatastore
    {
        private static RailGraphDatastore _instance;

        private readonly Dictionary<int, RailNode> _nodes = new();

        private RailGraphDatastore()
        {
            _instance = this;
        }

        public static RailGraphDatastore Instance => _instance ??= new RailGraphDatastore();


        
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