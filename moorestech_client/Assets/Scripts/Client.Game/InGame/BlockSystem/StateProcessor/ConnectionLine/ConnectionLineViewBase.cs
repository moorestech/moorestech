using System.Collections.Generic;
using System.Linq;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    /// ブロック間の接続ライン群を管理・表示するビューの共通基底
    /// Common base for views that manage and display connection lines between blocks
    /// </summary>
    public abstract class ConnectionLineViewBase<TElement> : MonoBehaviour where TElement : MonoBehaviour, IConnectionLineViewElement
    {
        private BlockInstanceId _myBlockInstanceId;
        private TElement _linePrefab;

        // 接続先InstanceId -> 生成したラインElement
        // Target InstanceId -> Generated line element
        private readonly Dictionary<BlockInstanceId, TElement> _activeLines = new();

        // ラインElementプレハブのAddressableアドレス
        // Addressable address of the line element prefab
        protected abstract string GetLinePrefabAddress();

        public void Initialize(BlockGameObject blockGameObject)
        {
            var prefab = AddressableLoader.LoadDefault<GameObject>(GetLinePrefabAddress());
            _linePrefab = prefab.GetComponent<TElement>();

            _myBlockInstanceId = blockGameObject.BlockInstanceId;
        }

        /// <summary>
        /// 接続ラインの表示を更新する
        /// Update the connection line display
        /// </summary>
        public void UpdateConnectionLines(BlockInstanceId[] partnerInstanceIds)
        {
            var newInstanceIds = new HashSet<BlockInstanceId>(partnerInstanceIds);

            // 不要になったラインを削除する
            // Remove lines that are no longer needed
            RemoveOldLines(newInstanceIds);

            // 新規接続のラインを作成する
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

                    var element = CreateLineElement(targetId);
                    _activeLines[targetId] = element;
                }
            }

            // 重複回避：InstanceId比較で小さい方のみ描画担当
            // Duplicate avoidance: Only the one with smaller InstanceId draws
            bool ShouldDrawLine(BlockInstanceId myId, BlockInstanceId targetId)
            {
                return myId.AsPrimitive() < targetId.AsPrimitive();
            }

            // ラインElementを生成する
            // Create the line element
            TElement CreateLineElement(BlockInstanceId targetId)
            {
                var element = Instantiate(_linePrefab, transform);
                element.SetLine(_myBlockInstanceId, targetId);
                return element;
            }

            #endregion
        }

        private void OnDestroy()
        {
            // すべてのラインを削除する
            // Destroy all lines
            foreach (var element in _activeLines.Values)
            {
                if (element != null) Destroy(element.gameObject);
            }
            _activeLines.Clear();
        }
    }
}
