using UnityEngine;

namespace Client.Skit.Context
{
    /// <summary>
    ///     スキットJSONの位置座標が基準にする原点（スポーン地点）。全位置コマンドはこの原点からの相対値で書く（ADR 0029）
    ///     Origin that skit JSON positions are relative to (the spawn point); every positional command is authored relative to it (ADR 0029)
    /// </summary>
    public class SkitOrigin
    {
        public Vector3 Position { get; }
        
        public SkitOrigin(Vector3 position)
        {
            Position = position;
        }
        
        public Vector3 ToWorld(Vector3 relativePosition)
        {
            return Position + relativePosition;
        }
    }
}
