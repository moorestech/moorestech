using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using Client.Tests.Common;
using Client.Tests.UIState.Fakes;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.UIState.DragDelete.Selection
{
    public class DeletePlacementSubscriptionTest
    {
        private GameObject _root;

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_root);

        [TestCase(false)]
        [TestCase(true)]
        public void 除外で演出購読を解放し再追加した選択だけ復元する(bool animationAlreadyFinished)
        {
            _root = new GameObject("placement-subscription");
            var block = _root.AddComponent<BlockGameObject>();
            var target = _root.AddComponent<PlacementSubscriptionTarget>();
            target.Init(block);
            var finished = TestReflection.GetField<Subject<BlockGameObject>>(block, "_onFinishedPlaceAnimation");
            if (animationAlreadyFinished) finished.OnNext(block);
            var selected = new DeleteSelectedTargetSet(new DeletePreviewResetRecovery());

            // 演出済みでも反復除外後に対象を保持しない
            // Repeated deselection retains no target even after animation has finished
            for (var iteration = 0; iteration < 20; iteration++)
            {
                selected.Add(target, target);
                Assert.IsTrue(finished.HasObservers);
                Assert.IsTrue(selected.TryRemove(target));
                Assert.IsFalse(finished.HasObservers);
            }
            var previews = target.Target.SetPreviewCount;
            finished.OnNext(block);
            Assert.AreEqual(previews, target.Target.SetPreviewCount);
            selected.Add(target, target);
            finished.OnNext(block);
            Assert.AreEqual(previews + 2, target.Target.SetPreviewCount);
            selected.RemoveAll(new IDeleteTarget[] { target });
            Assert.IsFalse(finished.HasObservers);
            selected.Add(target, target);
            selected.Clear();
            Assert.IsFalse(finished.HasObservers);
            selected.DrainReleases("test target unexpectedly destroyed");
        }
    }

    // 購読は本番のブロック通知を使い、描画だけFakeへ委譲する
    // Use the production block notification and delegate only rendering to the fake
    public class PlacementSubscriptionTarget : BlockGameObjectChild, IDeleteTarget
    {
        public readonly FakeDeleteTarget Target = new();
        void IDeleteTarget.SetRemovePreviewing() => Target.SetRemovePreviewing();
        void IDeleteTarget.ResetMaterial() => Target.ResetMaterial();
        object IDeleteTarget.GetDeleteTargetKey() => this;
        object IDeleteTarget.GetPreviewResetKey() => this;
        string IDeleteTarget.GetDestructionCategory() => Target.Category;
        bool IDeleteTarget.IsRemovable(out LocalizationKey? reason) => Target.IsRemovable(out reason);
        void IDeleteTarget.Delete() => Target.Delete();
        void IDeleteTarget.CollectRemovedObjects(RemovedObjectCollector collector) => Target.CollectRemovedObjects(collector);
    }
}
