using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.BeltConveyor
{
    /// <summary>
    /// ベルトコンベアのアイテムパス情報を保持するコンポーネント
    /// Component that holds item path information for belt conveyor
    /// </summary>
    public class BeltConveyorItemPath : MonoBehaviour
    {
        public IReadOnlyList<BeltConveyorItemPathData> Paths => paths;
        [SerializeField] private List<BeltConveyorItemPathData> paths = new();

        public BezierPath DefaultPath => defaultPath;
        [SerializeField] private BezierPath defaultPath;

        /// <summary>
        /// startIdとgoalIdの組み合わせに対応するパスを取得（見つからない場合はデフォルトパス）
        /// Get path by startId and goalId combination (returns default path if not found)
        /// </summary>
        public BezierPath GetPath(string startId, string goalId)
        {
            // 両方がnullまたは空の場合はデフォルトパスを返す
            // Return default path if both are null or empty
            if (string.IsNullOrEmpty(startId) && string.IsNullOrEmpty(goalId))
            {
                return defaultPath;
            }

            return FindPathByIds();

            #region Internal

            BezierPath FindPathByIds()
            {
                // startIdとgoalIdの両方が一致するパスを検索
                // Search for path matching both startId and goalId
                foreach (var path in paths)
                {
                    if (path.StartId == startId && path.GoalId == goalId)
                    {
                        return path.BezierPath;
                    }
                }

                return defaultPath;
            }

            #endregion
        }

        /// <summary>
        /// 進捗割合（0.0-1.0）に対応するワールド座標を取得
        /// Get world position for given progress ratio (0.0-1.0)
        /// </summary>
        public Vector3 GetWorldPosition(string startId, string goalId, float progressPercent)
        {
            var path = GetPath(startId, goalId);

            // 進捗割合をそのままBezierのtとして使う
            // Use progress ratio directly as Bezier t
            Vector3 localPosition = path.GetPoint(progressPercent);
            return transform.TransformPoint(localPosition);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 新しいパスを追加
        /// Add new path
        /// </summary>
        public void AddPath(string startId, string goalId)
        {
            var newPath = new BeltConveyorItemPathData(startId, goalId);
            newPath.BezierPath.SetDefault(Vector3.zero, Vector3.forward);
            paths.Add(newPath);
        }

        /// <summary>
        /// パスを削除
        /// Remove path
        /// </summary>
        public void RemovePath(int index)
        {
            if (index >= 0 && index < paths.Count)
            {
                paths.RemoveAt(index);
            }
        }

        /// <summary>
        /// デフォルトパスを初期化
        /// Initialize default path
        /// </summary>
        public void InitializeDefaultPath()
        {
            defaultPath = new BezierPath();
            defaultPath.SetDefault(Vector3.zero, Vector3.forward);
        }
#endif
    }
}
