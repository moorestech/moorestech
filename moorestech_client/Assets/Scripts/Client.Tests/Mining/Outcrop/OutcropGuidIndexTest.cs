using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.Outcrop;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Mining.Outcrop
{
    public class OutcropGuidIndexTest
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _objects)
                UnityEngine.Object.DestroyImmediate(gameObject);
            _objects.Clear();
        }

        [Test]
        public void 同じveinGuidだけから検索位置に最も近い露頭を返し未登録Guidはnullになる()
        {
            var targetGuid = Guid.Parse("11111111-0000-0000-0000-000000000001");
            var otherGuid = Guid.Parse("11111111-0000-0000-0000-000000000002");
            var index = new OutcropGuidIndex();
            var far = CreateOutcrop("far", new Vector3(10, 0, 0));
            var nearest = CreateOutcrop("nearest", new Vector3(2, 0, 0));
            var wrongGuidCloser = CreateOutcrop("wrong-guid", new Vector3(1, 0, 0));

            // GUID絞込と距離を検証
            // Verify GUID filter and distance
            index.Add(targetGuid, far);
            index.Add(otherGuid, wrongGuidCloser);
            index.Add(targetGuid, nearest);

            Assert.AreSame(nearest, index.SearchNearest(targetGuid, Vector3.zero));
            Assert.IsNull(index.SearchNearest(Guid.NewGuid(), Vector3.zero));

            #region Internal

            OutcropGameObject CreateOutcrop(string name, Vector3 position)
            {
                var gameObject = new GameObject(name);
                gameObject.transform.position = position;
                _objects.Add(gameObject);
                return gameObject.AddComponent<OutcropGameObject>();
            }

            #endregion
        }
    }
}
