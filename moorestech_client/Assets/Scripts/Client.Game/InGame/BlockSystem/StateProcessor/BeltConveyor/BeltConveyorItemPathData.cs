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
        public string StartGuid => startGuid;
        [SerializeField] private string startGuid;

        public string GoalGuid => goalGuid;
        [SerializeField] private string goalGuid;

        public BezierPath BezierPath => bezierPath;
        [SerializeField] private BezierPath bezierPath;

        public BeltConveyorItemPathData(string startGuid, string goalGuid)
        {
            this.startGuid = startGuid;
            this.goalGuid = goalGuid;
            bezierPath = new BezierPath();
        }
    }
}
