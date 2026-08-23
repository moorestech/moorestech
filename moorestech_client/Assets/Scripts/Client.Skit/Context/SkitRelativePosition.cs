using UnityEngine;

namespace Client.Skit.Context
{
    /// <summary>
    ///     スキットJSONが持つスポーン基準の相対位置。ワールド座標はSkitOriginを渡さないと取り出せない（ADR 0029）
    ///     A spawn-relative position from skit JSON; world space is reachable only by handing it a SkitOrigin (ADR 0029)
    /// </summary>
    public readonly struct SkitRelativePosition
    {
        private readonly Vector3 _relativePosition;
        
        public SkitRelativePosition(Vector3 relativePosition)
        {
            _relativePosition = relativePosition;
        }
        
        public Vector3 ToWorld(SkitOrigin origin)
        {
            return origin.ToWorld(_relativePosition);
        }
    }
}
