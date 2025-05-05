using Client.Game.InGame.CraftTree.TreeView;
using Game.CraftTree;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.Target
{
    public class CraftTreeTargetViewManager : MonoBehaviour
    {
        [SerializeField] private CraftTreeTargetView targetView;
        
        public void SetCurrentCraftTree(CraftTreeNode rootNode)
        {
            // 最も深い未完了のノードを取得する
            var targetNodes = CraftTreeUpdater.GetCurrentTarget(rootNode);
            targetView.SetTarget(targetNodes);
        }
    }
}