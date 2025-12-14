using System.Collections.Generic;
using Game.Gear.Common;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    /// <summary>
    /// エディタ専用のギアシミュレーター
    /// Editor-only gear simulator
    /// </summary>
    public class GearStateChangeProcessorSimulator : MonoBehaviour
    {
        [SerializeField] private GearStateChangeProcessor targetProcessor;
        [SerializeField] private bool isSimulating;
        [SerializeField] private float simulateRpm = 60;
        [SerializeField] private bool simulateIsClockwise = true;

        private Dictionary<Transform, Quaternion> _initialRotations = new();

#if UNITY_EDITOR
        // 静的なシミュレーター管理リスト
        // Static simulator management list
        private static readonly List<GearStateChangeProcessorSimulator> _activeSimulators = new();
        private static bool _isEditorUpdateRegistered = false;
#endif

        private void OnValidate()
        {
            // targetProcessorが未設定の場合は自動取得
            // Auto-acquire targetProcessor if not set
            if (targetProcessor == null)
            {
                targetProcessor = GetComponent<GearStateChangeProcessor>();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// エディタ更新コールバック（静的）
        /// Editor update callback (static)
        /// </summary>
        private static void OnEditorUpdate()
        {
            // アクティブな全シミュレーターを更新
            // Update all active simulators
            for (int i = _activeSimulators.Count - 1; i >= 0; i--)
            {
                var simulator = _activeSimulators[i];

                // nullチェック（GameObject削除済みの場合）
                // Null check (in case GameObject is deleted)
                if (simulator == null)
                {
                    _activeSimulators.RemoveAt(i);
                    continue;
                }

                // シミュレーション実行
                // Execute simulation
                if (simulator.isSimulating && simulator.targetProcessor != null)
                {
                    var state = new GearStateDetail(
                        simulator.simulateIsClockwise,
                        simulator.simulateRpm,
                        0,
                        GearNetworkInfo.CreateEmpty()
                    );
                    simulator.targetProcessor.Rotate(state);
                }
            }

            // アクティブなシミュレーターがなくなったらコールバック解除
            // Unregister callback if no active simulators
            if (_activeSimulators.Count == 0)
            {
                EditorApplication.update -= OnEditorUpdate;
                _isEditorUpdateRegistered = false;
            }
        }
#endif

        /// <summary>
        /// シミュレーション開始
        /// Start simulation
        /// </summary>
        public void StartSimulation()
        {
            if (targetProcessor == null)
            {
                Debug.LogWarning("Target Processor is not set.");
                return;
            }

            // 初期回転を保存
            // Save initial rotations
            _initialRotations.Clear();
            foreach (var rotationInfo in targetProcessor.RotationInfos)
            {
                if (rotationInfo.RotationTransform != null)
                {
                    _initialRotations.Add(rotationInfo.RotationTransform, rotationInfo.RotationTransform.rotation);
                }
            }

            isSimulating = true;

#if UNITY_EDITOR
            // 静的リストに追加
            // Add to static list
            if (!_activeSimulators.Contains(this))
            {
                _activeSimulators.Add(this);
            }

            // 初回のみEditorApplication.updateに登録
            // Register to EditorApplication.update only on first time
            if (!_isEditorUpdateRegistered)
            {
                EditorApplication.update += OnEditorUpdate;
                _isEditorUpdateRegistered = true;
            }
#endif
        }

        /// <summary>
        /// シミュレーション停止
        /// Stop simulation
        /// </summary>
        public void StopSimulation()
        {
            if (targetProcessor == null) return;

            isSimulating = false;

            // 初期回転に戻す
            // Restore initial rotations
            foreach (var rotationInfo in targetProcessor.RotationInfos)
            {
                if (rotationInfo.RotationTransform != null &&
                    _initialRotations.TryGetValue(rotationInfo.RotationTransform, out var initialRotation))
                {
                    rotationInfo.RotationTransform.rotation = initialRotation;
                }
            }

#if UNITY_EDITOR
            // 静的リストから削除
            // Remove from static list
            _activeSimulators.Remove(this);
#endif
        }

        private void OnDestroy()
        {
            // GameObject削除時にシミュレーション停止
            // Stop simulation when GameObject is destroyed
            if (isSimulating)
            {
                StopSimulation();
            }
        }

        public GearStateChangeProcessor TargetProcessor => targetProcessor;
        public bool IsSimulating => isSimulating;
        public float SimulateRpm
        {
            get => simulateRpm;
            set => simulateRpm = value;
        }
        public bool SimulateIsClockwise
        {
            get => simulateIsClockwise;
            set => simulateIsClockwise = value;
        }
    }
}
