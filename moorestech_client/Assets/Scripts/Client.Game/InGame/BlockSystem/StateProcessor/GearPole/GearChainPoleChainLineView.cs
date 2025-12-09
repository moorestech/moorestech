using System.Collections.Generic;
using System.Linq;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.GearPole
{
    /// <summary>
    /// GearChainPoleのチェーン接続を視覚的に表示するコンポーネント
    /// Component for visually displaying chain connections of GearChainPole
    /// </summary>
    public class GearChainPoleChainLineView : MonoBehaviour
    {
        private const string ChainLinePrefabAddress = "Vanilla/Block/Util/GearChainLine";

        private BlockInstanceId _myBlockInstanceId;
        private GearChainPoleChainLineViewElement _chainLinePrefab;

        // 接続先InstanceId -> 生成したラインElement
        // Target InstanceId -> Generated line element
        private readonly Dictionary<BlockInstanceId, GearChainPoleChainLineViewElement> _activeLines = new();

        public void Initialize(BlockGameObject blockGameObject)
        {
            var prefab = AddressableLoader.LoadDefault<GameObject>(ChainLinePrefabAddress);
            _chainLinePrefab = prefab.GetComponent<GearChainPoleChainLineViewElement>();

            _myBlockInstanceId = blockGameObject.BlockInstanceId;
        }

        /// <summary>
        /// チェーン接続の表示を更新する
        /// Update the chain connection display
        /// </summary>
        public void UpdateChainLines(BlockInstanceId[] partnerInstanceIds)
        {
            var newInstanceIds = new HashSet<BlockInstanceId>(partnerInstanceIds);

            // 削除対象を特定して削除
            // Identify and remove lines that are no longer needed
            RemoveOldLines(newInstanceIds);

            // 新規接続のラインを作成
            // Create lines for new connections
            AddNewLines(partnerInstanceIds);

            #region Internal

            void RemoveOldLines(HashSet<BlockInstanceId> currentInstanceIds)
            {
                var idsToRemove = _activeLines.Keys
                    .Where(id => !currentInstanceIds.Contains(id))
                    .ToList();

                foreach (var id in idsToRemove)
                {
                    if (_activeLines.TryGetValue(id, out var element))
                    {
                        Destroy(element.gameObject);
                    }
                    _activeLines.Remove(id);
                }
            }

            void AddNewLines(BlockInstanceId[] instanceIds)
            {
                foreach (var targetId in instanceIds)
                {
                    if (_activeLines.ContainsKey(targetId)) continue;
                    if (!ShouldDrawLine(_myBlockInstanceId, targetId)) continue;

                    var element = CreateChainLineElement(targetId);
                    _activeLines[targetId] = element;
                }
            }
            
            // 重複回避：InstanceId比較で小さい方のみ描画担当
            // Duplicate avoidance: Only the one with smaller InstanceId draws
            bool ShouldDrawLine(BlockInstanceId myId, BlockInstanceId targetId)
            {
                return myId.AsPrimitive() < targetId.AsPrimitive();
            }
            
            // チェーンラインElementを生成する
            // Create chain line element
            GearChainPoleChainLineViewElement CreateChainLineElement(BlockInstanceId targetId)
            {
                var element = Instantiate(_chainLinePrefab, transform);
                element.SetLine(_myBlockInstanceId, targetId);
                return element;
            }
            

            #endregion
        }

        private void OnDestroy()
        {
            // すべてのラインを削除
            // Destroy all lines
            foreach (var element in _activeLines.Values)
            {
                if (element != null) Destroy(element.gameObject);
            }
            _activeLines.Clear();
        }
    }
}
