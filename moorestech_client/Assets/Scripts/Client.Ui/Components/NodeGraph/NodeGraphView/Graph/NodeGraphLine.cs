using UnityEngine;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// 子の点を始点にした線（始点・長さ・角度。Web の TreeLine）
    /// A line rooted at the child point (origin, length, angle; Web TreeLine)
    /// </summary>
    internal readonly struct NodeGraphLine
    {
        public Vector2 Origin { get; }
        public float Length { get; }
        public float AngleDegrees { get; }

        public NodeGraphLine(Vector2 origin, float length, float angleDegrees)
        {
            Origin = origin;
            Length = length;
            AngleDegrees = angleDegrees;
        }
    }
}
