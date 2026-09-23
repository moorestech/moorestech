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
        internal void Register(int kind, GraphicsBuffer positions, GraphicsBuffer offsets, float cubeEdge)
        {
            int index = Array.BinarySearch(Kinds, kind);
            if (Materials[index] != null) return;
            // dense索引は固定し、実際に搬送されたkindだけ画像とmaterialを登録する。
            // Keep dense indices fixed and register images/materials only for transported kinds.
            images.TryGetItemView(new ItemId(kind), out var view);
            var texture = view?.ItemTexture;
            bool reportMissing = texture == null && diagnosedMissing.Add(kind);
            var material = Create(kind, texture, shader, reportMissing);
            bool registered = false;
            try
            {
                material.SetBuffer("_Positions", positions); material.SetBuffer("_Offsets", offsets);
                material.SetInt("_KindIndex", index); material.SetFloat("_CubeEdge", cubeEdge);
                Materials[index] = material;
                registered = true;
            }
            finally { if (!registered) DestroyResource(material); }
        }
        internal static Material Create(int kind, Texture texture, Shader shader, bool reportMissing)
        {
            var material = new Material(shader);
            bool initialized = false;
            try
            {
                material.enableInstancing = true;
                if (texture == null)
                {
                    if (reportMissing) Debug.LogWarning($"[BeltItemMaterials] Missing image for item kind {kind}; using magenta cube.");
                    material.SetColor("_BaseColor", Color.magenta);
                }
                else material.SetTexture("_BaseMap", texture);
                initialized = true;
                return material;
            }
            finally { if (!initialized) DestroyResource(material); }
        }
        public void Dispose() { foreach (var material in Materials) DestroyResource(material); }
#if UNITY_EDITOR
        internal static void DestroyResource(UnityEngine.Object resource)
        {
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(resource);
            else UnityEngine.Object.Destroy(resource);
        }
#else
        internal static void DestroyResource(UnityEngine.Object resource) => UnityEngine.Object.Destroy(resource);
#endif
    }
}
