using Client.Common;
using Client.Game.InGame.Interact.Outline;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Interact
{
    public class RuntimeOutlineFactoryTest
    {
        [Test]
        public void 最近傍LODのメッシュがOutlineレイヤに複製され非活性で返る()
        {
            var root = new GameObject("Root");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(1f, 2f, 3f);
            visual.GetComponent<MeshRenderer>().sharedMaterials = new Material[2];

            var outline = RuntimeOutlineFactory.Create(root);

            Assert.AreEqual(root.transform, outline.transform.parent);
            Assert.IsFalse(outline.activeSelf);
            Assert.AreEqual(LayerConst.OutlineLayer, outline.layer);
            var copied = outline.GetComponentInChildren<MeshRenderer>(true);
            Assert.AreEqual(LayerConst.OutlineLayer, copied.gameObject.layer);
            Assert.AreEqual(visual.GetComponent<MeshFilter>().sharedMesh, copied.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreEqual(2, copied.sharedMaterials.Length);
            Assert.AreSame(MaterialConst.GetInteractOutlineMaterial(), copied.sharedMaterials[0]);
            Assert.AreEqual(visual.transform.position, copied.transform.position);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void LODGroupがある場合はLOD0のレンダラーだけが複製されLOD1は複製されない()
        {
            var root = new GameObject("Root");

            var lod0 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lod0.name = "Lod0Visual";
            lod0.transform.SetParent(root.transform, false);

            var lod1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lod1.name = "Lod1Visual";
            lod1.transform.SetParent(root.transform, false);

            var lodGroup = root.AddComponent<LODGroup>();
            var lod0Renderer = lod0.GetComponent<MeshRenderer>();
            var lod1Renderer = lod1.GetComponent<MeshRenderer>();
            lodGroup.SetLODs(new[]
            {
                new LOD(0.5f, new Renderer[] { lod0Renderer }),
                new LOD(0.01f, new Renderer[] { lod1Renderer })
            });

            var outline = RuntimeOutlineFactory.Create(root);

            var copiedRenderers = outline.GetComponentsInChildren<MeshRenderer>(true);
            Assert.AreEqual(1, copiedRenderers.Length);
            Assert.AreEqual(lod0Renderer.name, copiedRenderers[0].name);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void 複製できるメッシュが無ければnullを返し空の輪郭を残さない()
        {
            var root = new GameObject("Root");

            var outline = RuntimeOutlineFactory.Create(root);

            Assert.IsNull(outline);
            Assert.AreEqual(0, root.transform.childCount);

            Object.DestroyImmediate(root);
        }
    }
}
