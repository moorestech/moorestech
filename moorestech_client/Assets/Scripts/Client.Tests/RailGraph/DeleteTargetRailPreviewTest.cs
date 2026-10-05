using Client.Game.InGame.Train.RailGraph;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.RailGraph
{
    /// <summary>
    ///     撤去後に寿命が切れたレール表示体への赤表示操作を検証する
    ///     Verifies preview calls after a rail view has been destroyed
    /// </summary>
    public class DeleteTargetRailPreviewTest
    {
        private GameObject _targetObject;
        private GameObject _chainObject;

        [TearDown]
        public void TearDown()
        {
            if (_targetObject != null) Object.DestroyImmediate(_targetObject);
            if (_chainObject != null) Object.DestroyImmediate(_chainObject);
        }

        [Test]
        public void PreviewCallsIgnoreDestroyedRailChain()
        {
            // 破棄後も管理参照が残る状態を作る
            // Keep the managed reference after destroying its Unity object
            _targetObject = new GameObject("RailTarget");
            _chainObject = new GameObject("RailChain");
            var target = _targetObject.AddComponent<DeleteTargetRail>();
            var chain = _chainObject.AddComponent<BezierRailChain>();
            target.SetParentBezierRailChain(chain);
            Object.DestroyImmediate(_chainObject);
            Assert.IsTrue(chain == null);

            // 赤表示も解除も破棄済みへ触れない
            // Neither red nor release accesses the destroyed view
            target.SetRemovePreviewing();
            target.ResetMaterial();
        }

        [Test]
        public void PreviewAfterEarlyRebuildReturnDoesNotRequireControllerFromRebuildTail()
        {
            _chainObject = new GameObject("EmptyRailChain");
            var chain = _chainObject.AddComponent<BezierRailChain>();
            LogAssert.Expect(LogType.Warning, "[BezierRailChain] rebuild skipped: module prefab missing");
            chain.Rebuild();

            // Rebuild早期終了後もプレビューを初期化できる
            // Preview can initialize even after Rebuild returns early
            chain.SetRemovePreviewing();
            chain.ResetMaterial();
        }
    }
}
