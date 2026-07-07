using System.Collections.Generic;
using System.Linq;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire
{
    /// <summary>
    /// 電力ワイヤーの接続を視覚的に表示するコンポーネント
    /// Component for visually displaying electric wire connections
    /// </summary>
    public class ElectricWireLineView : MonoBehaviour
    {
        private const string WireLinePrefabAddress = "Vanilla/Block/Util/ElectricWireLine";

        private BlockInstanceId _myBlockInstanceId;
        private ElectricWireLineViewElement _wireLinePrefab;

        // 接続先InstanceId -> 生成したワイヤーElement
        // Target InstanceId -> Generated wire element
        private readonly Dictionary<BlockInstanceId, ElectricWireLineViewElement> _activeLines = new();

        public void Initialize(BlockGameObject blockGameObject)
        {
            var prefab = AddressableLoader.LoadDefault<GameObject>(WireLinePrefabAddress);
            _wireLinePrefab = prefab.GetComponent<ElectricWireLineViewElement>();

            _myBlockInstanceId = blockGameObject.BlockInstanceId;
        }

        /// <summary>
        /// ワイヤー接続の表示を更新する
        /// Update the wire connection display
        /// </summary>
        public void UpdateWireLines(BlockInstanceId[] partnerInstanceIds)
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

                    var element = CreateWireLineElement(targetId);
                    _activeLines[targetId] = element;
                }
            }

            // 重複回避：InstanceId比較で小さい方のみ描画担当
            // Duplicate avoidance: Only the one with smaller InstanceId draws
            bool ShouldDrawLine(BlockInstanceId myId, BlockInstanceId targetId)
            {
                return myId.AsPrimitive() < targetId.AsPrimitive();
            }

            // ワイヤーラインElementを生成する
            // Create wire line element
            ElectricWireLineViewElement CreateWireLineElement(BlockInstanceId targetId)
            {
                var element = Instantiate(_wireLinePrefab, transform);
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
