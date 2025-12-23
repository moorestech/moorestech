using System;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.BeltConveyor
{
    /// <summary>
    /// 単一のアイテムパスデータ（StartIdとGoalIdの組み合わせで識別）
    /// Single item path data (identified by StartId and GoalId combination)
    /// </summary>
    [Serializable]
    public class BeltConveyorItemPathData
    {
        public string StartId => startId;
        [SerializeField] private string startId;

        public string GoalId => goalId;
        [SerializeField] private string goalId;

        public BezierPath BezierPath => bezierPath;
        [SerializeField] private BezierPath bezierPath;

        public BeltConveyorItemPathData(string startId, string goalId)
        {
            this.startId = startId;
            this.goalId = goalId;
            bezierPath = new BezierPath();
        }

#if UNITY_EDITOR
        public void SetStartId(string value) => startId = value;
        public void SetGoalId(string value) => goalId = value;
#endif
    }
}
