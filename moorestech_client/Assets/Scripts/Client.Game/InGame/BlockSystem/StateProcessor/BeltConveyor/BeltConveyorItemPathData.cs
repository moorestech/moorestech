using System;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.BeltConveyor
{
    /// <summary>
    /// 単一のアイテムパスデータ
    /// Single item path data
    /// </summary>
    [Serializable]
    public class BeltConveyorItemPathData
    {
        public string PathId => pathId;
        [SerializeField] private string pathId;

        public BezierPath BezierPath => bezierPath;
        [SerializeField] private BezierPath bezierPath;

        public BeltConveyorItemPathData(string pathId)
        {
            this.pathId = pathId;
            bezierPath = new BezierPath();
        }

#if UNITY_EDITOR
        public void SetPathId(string value) => pathId = value;
#endif
    }
}
