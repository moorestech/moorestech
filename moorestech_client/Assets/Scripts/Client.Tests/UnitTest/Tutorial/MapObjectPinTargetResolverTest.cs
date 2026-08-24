using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Tutorial;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.UnitTest.Tutorial
{
    public class MapObjectPinTargetResolverTest
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void 後着中の空振りは欠落報告しない()
        {
            var source = new FakeTargetSource(false, null);
            var resolver = new MapObjectPinTargetResolver(source);

            var found = resolver.TryResolve(new HashSet<Guid>(), Vector3.zero, false, out _, out var shouldReportMissing);

            Assert.IsFalse(found);
            Assert.IsFalse(shouldReportMissing);
        }

        [Test]
        public void 全量成功後の初回空振りだけ欠落報告する()
        {
            var source = new FakeTargetSource(true, null);
            var resolver = new MapObjectPinTargetResolver(source);

            resolver.TryResolve(new HashSet<Guid>(), Vector3.zero, false, out _, out var firstReport);
            resolver.TryResolve(new HashSet<Guid>(), Vector3.zero, true, out _, out var repeatedReport);

            Assert.IsTrue(firstReport);
            Assert.IsFalse(repeatedReport);
        }

        [Test]
        public void 対象を解決できれば欠落報告しない()
        {
            _root = new GameObject("MapObjectPinTargetResolverTest");
            var target = _root.AddComponent<MapObjectGameObject>();
            var source = new FakeTargetSource(false, target);
            var resolver = new MapObjectPinTargetResolver(source);

            var found = resolver.TryResolve(new HashSet<Guid>(), Vector3.zero, false, out var actual, out var shouldReportMissing);

            Assert.IsTrue(found);
            Assert.AreSame(target, actual);
            Assert.IsFalse(shouldReportMissing);
        }

        private sealed class FakeTargetSource : IMapObjectPinTargetSource
        {
            private readonly MapObjectGameObject _target;
            public IReadOnlyReactiveProperty<bool> IsAllInstantiated { get; }

            public FakeTargetSource(bool isAllInstantiated, MapObjectGameObject target)
            {
                IsAllInstantiated = new ReactiveProperty<bool>(isAllInstantiated);
                _target = target;
            }

            public MapObjectGameObject SearchNearestMapObject(HashSet<Guid> mapObjectGuids, Vector3 position)
            {
                return _target;
            }
        }
    }
}
