using System.Collections.Generic;
using UnityEngine;

namespace Client.Common.PrefabPostProcess
{
    /// <summary>
    /// 後処理どうしで共有する状態。使用済みRendererの印と、マテリアルの別版のキャッシュを持つ
    /// State shared across post-process steps: claimed renderers and the material variant cache
    /// </summary>
    public sealed class PrefabPostProcessContext
    {
        public readonly MaterialVariantCache MaterialVariants = new();
        private readonly HashSet<Renderer> _claimedRenderers = new();

        // 先に処理した後処理が使ったRendererに印を付け、後の後処理が触らないようにする
        // Mark renderers consumed by an earlier step so later steps leave them alone
        public void Claim(Renderer renderer)
        {
            _claimedRenderers.Add(renderer);
        }

        public bool IsClaimed(Renderer renderer)
        {
            return _claimedRenderers.Contains(renderer);
        }
    }
}
