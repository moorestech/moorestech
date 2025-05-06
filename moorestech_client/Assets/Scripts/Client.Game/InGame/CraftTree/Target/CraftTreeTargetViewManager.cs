using Client.Game.InGame.CraftTree.TreeView;
using Game.CraftTree.Models;
using Game.CraftTree.Models;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.Target
{
    public class CraftTreeTargetViewManager : MonoBehaviour
    {
        [SerializeField] private CraftTreeTargetView targetView;
        
        private CraftTreeUpdater _craftTreeUpdater;
        
        public void Initialize(CraftTreeUpdater craftTreeUpdater)
        {
            _craftTreeUpdater = craftTreeUpdater;
            _craftTreeUpdater.OnUpdateCraftTree.Subscribe(SetCurrentCraftTree);
        }
        
        public void SetCurrentCraftTree(CraftTreeNode rootNode)
        {
            // 最も深い未完了のノードを取得する
            var targetNodes = CraftTreeUpdater.GetCurrentTarget(rootNode);
            targetView.SetTarget(targetNodes);
        }
        
        public void ClearTarget()
        {
            // 既存のターゲットを削除
            targetView.ClearTarget();
        }
    }
}