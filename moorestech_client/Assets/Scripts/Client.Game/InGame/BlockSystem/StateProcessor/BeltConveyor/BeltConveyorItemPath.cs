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
        /// pathIdに対応するパスを取得（見つからない場合はデフォルトパス）
        /// Get path by pathId (returns default path if not found)
        /// </summary>
        public BezierPath GetPath(string pathId)
        {
            // pathIdがnullまたは空の場合はデフォルトパスを返す
            // Return default path if pathId is null or empty
            if (string.IsNullOrEmpty(pathId))
            {
                return defaultPath;
            }

            return FindPathById();

            #region Internal

            BezierPath FindPathById()
            {
                // pathIdに一致するパスを検索
                // Search for path matching pathId
                foreach (var path in paths)
                {
                    if (path.PathId == pathId)
                    {
                        return path.BezierPath;
                    }
                }

                return defaultPath;
            }

            #endregion
        }

        /// <summary>
        /// RemainingPercent（0.0-1.0）に対応するワールド座標を取得
        /// Get world position for given RemainingPercent (0.0-1.0)
        /// </summary>
        public Vector3 GetWorldPosition(string pathId, float remainingPercent)
        {
            var path = GetPath(pathId);

            // RemainingPercentは1.0から0.0に減少する
            // RemainingPercent decreases from 1.0 to 0.0
            // パスのtは0.0から1.0で進む
            // Path t progresses from 0.0 to 1.0
            float t = 1f - remainingPercent;

            Vector3 localPosition = path.GetPoint(t);
            return transform.TransformPoint(localPosition);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 新しいパスを追加
        /// Add new path
        /// </summary>
        public void AddPath(string pathId)
        {
            var newPath = new BeltConveyorItemPathData(pathId);
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
