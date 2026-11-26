using UnityEngine;

namespace Client.Common.PrefabPostProcess.MeshMerge
{
    /// <summary>
    /// まとめたRendererの置き場所の名前・親・姿勢
    /// Name, parent and pose of the host for a merged renderer
    /// </summary>
    public readonly struct MergedRendererPlacement
    {
        public readonly string Name;
        public readonly Transform Parent;
        public readonly Vector3 LocalPosition;
        public readonly Quaternion LocalRotation;

        public MergedRendererPlacement(string name, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            Name = name;
            Parent = parent;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
        }
    }
}
