using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Core.Master;
using UnityEngine;
namespace Client.Game.InGame.BeltSegment.Rendering
{
    internal sealed class BeltItemMaterials : IDisposable
    {
        internal readonly int[] Kinds;
        internal readonly Material[] Materials;
        private readonly ItemImageContainer images;
        private readonly Shader shader;
        private readonly HashSet<int> diagnosedMissing;
        internal BeltItemMaterials(ItemId[] ids, ItemImageContainer images, Shader shader, HashSet<int> diagnosedMissing)
        {
            this.images = images; this.shader = shader; this.diagnosedMissing = diagnosedMissing;
            Kinds = new int[ids.Length]; Materials = new Material[ids.Length];
            Array.Sort(ids, (a, b) => a.AsPrimitive().CompareTo(b.AsPrimitive()));
            for (int i = 0; i < ids.Length; i++) Kinds[i] = ids[i].AsPrimitive();
        }
        internal int Register(int kind)
        {
            int index = Array.BinarySearch(Kinds, kind);
            if (Materials[index] != null) return -1;
            // dense索引は固定し、実際に搬送されたkindだけ画像とmaterialを登録する。
            // Keep dense indices fixed and register images/materials only for transported kinds.
            var texture = images.GetItemView(new ItemId(kind))?.ItemTexture;
            bool reportMissing = texture == null && diagnosedMissing.Add(kind);
            Materials[index] = Create(kind, texture, shader, reportMissing);
            return index;
        }
        internal static Material Create(int kind, Texture texture, Shader shader, bool reportMissing)
        {
            var material = new Material(shader) { enableInstancing = true };
            if (texture == null)
            {
                if (reportMissing) Debug.LogWarning($"[BeltItemMaterials] Missing image for item kind {kind}; using magenta cube.");
                material.SetColor("_BaseColor", Color.magenta);
            }
            else material.SetTexture("_BaseMap", texture);
            return material;
        }
        public void Dispose() { foreach (var material in Materials) Destroy(material); }
        internal static void Destroy(UnityEngine.Object resource)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(resource); return; }
#endif
            UnityEngine.Object.Destroy(resource);
        }
    }
}
