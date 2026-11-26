using System.Collections.Generic;
using Client.Common.PrefabPostProcess;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PrefabPostProcess
{
    public class PrefabPostProcessPipelineTest
    {
        private readonly List<GameObject> _owned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _owned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _owned.Clear();
        }

        [Test]
        public void 対象の後処理が無ければ元のprefabをそのまま返す()
        {
            var source = Own(new GameObject("source"));
            var pipeline = new PrefabPostProcessPipeline("TestTemplates", new IPrefabPostProcessor[] { new RenameChildProcessor(false) });

            Assert.AreSame(source, pipeline.Process(source));
        }

        [Test]
        public void 対象なら非アクティブの親の下の複製を加工し元は変えない()
        {
            var source = Own(new GameObject("source"));
            new GameObject("child").transform.SetParent(source.transform, false);
            var pipeline = new PrefabPostProcessPipeline("TestTemplates", new IPrefabPostProcessor[] { new RenameChildProcessor(true) });

            var template = pipeline.Process(source);
            Own(template.transform.parent.gameObject);

            Assert.AreNotSame(source, template);
            Assert.AreEqual("source", template.name);
            Assert.AreEqual("renamed", template.transform.GetChild(0).name);
            Assert.AreEqual("child", source.transform.GetChild(0).name);
            // 複製自体は有効のまま、親が非アクティブなので動き出さない
            // The clone stays active itself but does not run because its parent is inactive
            Assert.IsTrue(template.activeSelf);
            Assert.IsFalse(template.activeInHierarchy);
        }

        [Test]
        public void 後処理は登録順に呼ばれ使用済みの印を共有する()
        {
            var source = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var claimer = new ClaimRendererProcessor();
            var observer = new ObserveClaimProcessor();
            var pipeline = new PrefabPostProcessPipeline("TestTemplates", new IPrefabPostProcessor[] { claimer, observer });

            var template = pipeline.Process(source);
            Own(template.transform.parent.gameObject);

            Assert.IsTrue(observer.SawClaimed);
        }

        private GameObject Own(GameObject go)
        {
            _owned.Add(go);
            return go;
        }

        private sealed class RenameChildProcessor : IPrefabPostProcessor
        {
            private readonly bool _isTarget;
            public RenameChildProcessor(bool isTarget) { _isTarget = isTarget; }
            public bool IsTarget(GameObject prefab) { return _isTarget; }
            public void Process(GameObject template, PrefabPostProcessContext context) { template.transform.GetChild(0).name = "renamed"; }
        }

        private sealed class ClaimRendererProcessor : IPrefabPostProcessor
        {
            public bool IsTarget(GameObject prefab) { return true; }
            public void Process(GameObject template, PrefabPostProcessContext context) { context.Claim(template.GetComponent<Renderer>()); }
        }

        private sealed class ObserveClaimProcessor : IPrefabPostProcessor
        {
            public bool SawClaimed { get; private set; }
            public bool IsTarget(GameObject prefab) { return true; }
            public void Process(GameObject template, PrefabPostProcessContext context) { SawClaimed = context.IsClaimed(template.GetComponent<Renderer>()); }
        }
    }
}
