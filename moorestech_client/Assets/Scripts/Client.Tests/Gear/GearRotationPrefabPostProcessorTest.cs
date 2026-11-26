using System.Collections.Generic;
using System.Linq;
using Client.Common.PrefabPostProcess;
using Client.Game.InGame.Block.GearRotation.PrefabPostProcess;
using Client.Game.InGame.BlockSystem.StateProcessor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.Gear
{
    public class GearRotationPrefabPostProcessorTest
    {
        private const string SettingsPath = "Assets/Asset/Common/Shader/GearRotation/GearRotationRenderSettings.asset";
        private const string BlockPrefabFolder = "Assets/AddressableResources/Block";

        // シェーダー回転へ移せずCPUで回し続ける部品。増えたら理由を確かめてからここへ足す
        // Parts that stay on CPU rotation; when this grows, check the reason before adding here
        private static readonly string[] ExpectedCpuParts =
        {
            "Ore_Crusher/stone 1",
            "Ore_Crusher/stone 2",
        };

        private readonly List<GameObject> _roots = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var root in _roots) Object.DestroyImmediate(root);
            _roots.Clear();
        }

        [Test]
        public void 回す部品は1つのRendererにまとまりシェーダー回転になる()
        {
            var (template, settings) = Process("Shaft.prefab");
            var info = (TransformRotationInfo)template.GetComponentInChildren<GearStateChangeProcessor>(true).RotationInfos[0];

            Assert.IsTrue(info.IsGpuDriven);
            Assert.AreEqual(0, info.RotationTransform.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled));
            var merged = info.RotationTransform.parent.Find(info.RotationTransform.name + " (GearRotation)").GetComponent<MeshRenderer>();
            Assert.IsTrue(merged.sharedMaterials.All(m => m.shader == settings.RotatingLitShader));

            // UV6には軸と速さの倍率、UV7にはブロック原点が入る
            // UV6 holds the axis and speed factor; UV7 holds the block origin
            var mesh = merged.GetComponent<MeshFilter>().sharedMesh;
            var axisAndSpeed = new List<Vector4>();
            mesh.GetUVs(GearRotationVertexWriter.AxisAndSpeedChannel, axisAndSpeed);
            var axis = GearWorldRotationSign.ToAxisVector(info.Axis);
            Assert.AreEqual(new Vector4(axis.x, axis.y, axis.z, info.SignedSpeedFactor), axisAndSpeed[0]);
            var originAndMode = new List<Vector4>();
            mesh.GetUVs(GearRotationVertexWriter.OriginAndModeChannel, originAndMode);
            Assert.AreEqual(merged.transform.InverseTransformPoint(template.transform.position), (Vector3)originAndMode[0]);
        }

        [Test]
        public void LODGroupの一覧はまとめたRendererに付け替わる()
        {
            var (template, _) = Process("gear belt conveyor.prefab");

            foreach (var lodGroup in template.GetComponentsInChildren<LODGroup>(true))
            foreach (var lod in lodGroup.GetLODs())
            {
                Assert.IsTrue(lod.renderers.All(r => r != null), $"{lodGroup.name} に消えたRendererが残っている");
            }
            var lod0 = template.GetComponentInChildren<LODGroup>(true).GetLODs()[0].renderers;
            Assert.IsTrue(lod0.Any(r => r.name.EndsWith(" (GearRotation)")));
        }

        [Test]
        public void 全ブロックで決めた部品以外はシェーダー回転になり消えた参照も残らない()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GearRotationRenderSettings>(SettingsPath);
            var pipeline = new PrefabPostProcessPipeline("TestTemplates", new IPrefabPostProcessor[] { new GearRotationPrefabPostProcessor(settings) });
            var cpuParts = new List<string>();
            var missingReferences = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { BlockPrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var template = pipeline.Process(prefab);
                if (template == prefab) continue;
                if (!_roots.Contains(template.transform.parent.gameObject)) _roots.Add(template.transform.parent.gameObject);

                foreach (var info in template.GetComponentsInChildren<GearStateChangeProcessor>(true).SelectMany(p => p.RotationInfos).OfType<TransformRotationInfo>())
                {
                    if (!info.IsGpuDriven) cpuParts.Add(prefab.name + "/" + info.RotationTransform.name);
                }
                CollectMissingReferences(template, missingReferences);
            }

            CollectionAssert.AreEquivalent(ExpectedCpuParts, cpuParts.Distinct());
            CollectionAssert.IsEmpty(missingReferences);
        }

        // 消したRendererやMeshFilterを参照したままのシリアライズ項目を探す
        // Find serialized fields still pointing at removed renderers or mesh filters
        private static void CollectMissingReferences(GameObject template, List<string> result)
        {
            foreach (var component in template.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var property = new SerializedObject(component).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (property.objectReferenceValue == null && !property.objectReferenceEntityIdValue.Equals(default(EntityId))) result.Add($"{template.name}/{component.name}.{component.GetType().Name}.{property.propertyPath}");
                }
            }
        }

        private (GameObject template, GearRotationRenderSettings settings) Process(string prefabFileName)
        {
            var settings = AssetDatabase.LoadAssetAtPath<GearRotationRenderSettings>(SettingsPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPrefabFolder + "/" + prefabFileName);
            var pipeline = new PrefabPostProcessPipeline("TestTemplates", new IPrefabPostProcessor[] { new GearRotationPrefabPostProcessor(settings) });
            var template = pipeline.Process(prefab);
            _roots.Add(template.transform.parent.gameObject);
            return (template, settings);
        }
    }
}
