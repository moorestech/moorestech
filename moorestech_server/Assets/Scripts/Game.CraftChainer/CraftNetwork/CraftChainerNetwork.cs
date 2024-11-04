using System.Collections.Generic;

namespace Game.CraftChainer.CraftNetwork
{
    public class CraftChainerNetwork
    {
        public List<ICraftChainerNode> CraftChainerNodes; // TODO ネットワークの異常検知
        
        public ICraftChainerNode GetInsertTargetNode(ICraftChainerNode currentNode, CraftChainerNodeId targetNodeId)
        {
            // TODO ルートを計算
            
            // TODO 次のノードを返す
        }
    }
}