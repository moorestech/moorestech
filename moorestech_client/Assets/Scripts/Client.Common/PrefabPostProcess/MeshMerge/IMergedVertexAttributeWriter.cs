using System.Collections.Generic;
using UnityEngine;

namespace Client.Common.PrefabPostProcess.MeshMerge
{
    /// <summary>
    /// まとめたメッシュの追加UVチャンネルに、元のRendererごとの値を書く
    /// Supplies per-source-renderer values for extra UV channels of a merged mesh
    /// </summary>
    public interface IMergedVertexAttributeWriter
    {
        IReadOnlyList<int> GetUvChannels();

        // 元Rendererの全頂点に同じ値を書く
        // The same value is written to every vertex of the source renderer
        Vector4 GetSourceValue(int uvChannel, MeshRenderer source);
    }
}
