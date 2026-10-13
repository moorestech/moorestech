#if UNITY_EDITOR
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Tests.PlaceSystem.Common.Preview
{
    public class PlacementGroundContactQueryCapacityTest
    {
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            Assert.AreNotEqual(Physics.defaultPhysicsScene, _scene.GetPhysicsScene());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in _scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(_scene);
        }

        [TestCase(0, false)]
        [TestCase(4, false)]
        [TestCase(8, false)]
        [TestCase(9, true)]
        public void CandidateCountAtAndAroundBufferCapacityCompletes(int candidateCount, bool expectedOverlap)
        {
            var preview = new GameObject("Preview");
            SceneManager.MoveGameObjectToScene(preview, _scene);
            preview.AddComponent<BoxCollider>().size = Vector3.one * 2f;
            var detector = preview.AddComponent<GroundCollisionDetector>();

            // 最後の候補だけを本物の地形にし、候補全件を走査する
            // Mark only the last candidate as ground so the query must scan them all.
            for (var index = 0; index < candidateCount; index++)
            {
                var candidate = new GameObject($"Candidate{index}");
                SceneManager.MoveGameObjectToScene(candidate, _scene);
                candidate.layer = LayerConst.GroundLayer;
                candidate.AddComponent<BoxCollider>().size = Vector3.one * 0.2f;
                if (expectedOverlap && index == candidateCount - 1) candidate.AddComponent<GroundGameObject>();
            }
            Physics.SyncTransforms();
            var actualCandidates = new Collider[32];
            Assert.AreEqual(candidateCount, _scene.GetPhysicsScene().OverlapBox(Vector3.zero, Vector3.one,
                actualCandidates, Quaternion.identity, 1 << LayerConst.GroundLayer, QueryTriggerInteraction.Collide));
            Assert.AreEqual(expectedOverlap, PlacementGroundContactQuery.HasOverlap(new[] { detector }, _scene));
        }
    }
}
#endif
