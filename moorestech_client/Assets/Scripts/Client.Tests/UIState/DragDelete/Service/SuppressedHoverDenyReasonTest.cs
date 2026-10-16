using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.BuildUndo;
using Client.Tests.UIState.Fakes;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UIState
{
    public class SuppressedHoverDenyReasonTest : UIStateTestFixtureBase
    {
        private readonly List<GameObject> _previousMainCameras = new();

        public override void TearDown()
        {
            base.TearDown();
            foreach (var previous in _previousMainCameras) previous.tag = "MainCamera";
            _previousMainCameras.Clear();
        }

        [Test]
        public void FailedPreviewResetStillShowsAimedTargetsDenial()
        {
            // 本番の照準と入力を通し、保持対象が空でも拒否理由を表示する
            // Use production aim and input to show denial even without a retained preview
            foreach (var previous in GameObject.FindGameObjectsWithTag("MainCamera"))
            {
                _previousMainCameras.Add(previous);
                previous.tag = "Untagged";
            }
            var camera = CreateComponent<Camera>("DeleteAimCamera");
            camera.tag = "MainCamera";
            Assert.AreSame(camera, Camera.main);
            camera.transform.position = new Vector3(10000, 10000, 10000);
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            var targetObject = CreateObject("DeleteTarget", true);
            targetObject.layer = LayerConst.BlockLayer;
            targetObject.transform.position = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint()).GetPoint(5);
            targetObject.AddComponent<BoxCollider>();
            var target = targetObject.AddComponent<HoverDenyReasonTarget>();
            target.Target.Removable = true;
            target.Target.ThrowOnReset = true;
            Physics.SyncTransforms();

            // 戻し失敗後に撤去拒否が届く順序を再現する
            // Reproduce a removal denial arriving after a failed preview reset
            var tooltip = new MouseCursorTooltipState();
            var service = new DeleteObjectService(new BuildOperationHistory(), tooltip, new FakeRemovalRestoreSender(), new Client.Game.InGame.UI.ProgressBar.ProgressBarState(), camera, new Client.Game.InGame.BlockSystem.PlaceSystem.Common.PlacementHeightOffset(), new Client.Game.InGame.UI.UIState.State.DragDelete.Box.DeleteBoxTargetResolver(null, null));
            service.Update(0, false);
            Assert.AreEqual(1, target.Target.SetPreviewCount);
            Assert.Throws<InvalidOperationException>(() => service.ClearAll());
            target.Target.Removable = false;
            target.Target.DenyReason = LocalizationKeys.Ui.Delete.RailHasVehicle;

            // 再保持を抑止しても毎フレームのHide後に理由を再表示する
            // Redisplay denial after each frame's Hide while suppressing reacquisition
            LogAssert.Expect(LogType.Warning, "[DragDelete] delete preview suppressed because the target's previous reset did not complete");
            service.Update(0, false);
            Assert.IsTrue(tooltip.GetPresentation().Visible);
            service.Update(0, false);
            Assert.AreEqual(LocalizationKeys.Ui.Delete.RailHasVehicle, tooltip.GetPresentation().Lines[0].Key);
            Assert.DoesNotThrow(() => service.ClearAll());
            Assert.AreEqual(1, target.Target.ResetCount);
            Assert.AreEqual(1, target.Target.SetPreviewCount);
        }
    }

    // コライダーから既存Fakeへ委譲し、照準経路を検証する
    // Delegate from a collider to the existing fake to exercise the aim path
    public class HoverDenyReasonTarget : MonoBehaviour, IDeleteTarget
    {
        public readonly FakeDeleteTarget Target = new();
        public IObservable<Client.Game.InGame.UI.UIState.State.DragDelete.Completion.DeleteCompletion> OnDeleteCompleted => Target.OnDeleteCompleted;
        public void SetRemovePreviewing() => Target.SetRemovePreviewing();
        public void ResetMaterial() => Target.ResetMaterial();
        public bool IsRemovable(out LocalizationKey? reason) => Target.IsRemovable(out reason);
        public void Delete() => Target.Delete();
        public void CollectRemovedObjects(RemovedObjectCollector collector) => Target.CollectRemovedObjects(collector);
        // 復元を共有する表示の寿命を識別する
        // Identify the lifetime of the shared preview view
        public object GetPreviewResetKey() => this;

        public object GetDeleteTargetKey() => Target.GetDeleteTargetKey();
        public string GetDestructionCategory() => Target.GetDestructionCategory();
    }
}
