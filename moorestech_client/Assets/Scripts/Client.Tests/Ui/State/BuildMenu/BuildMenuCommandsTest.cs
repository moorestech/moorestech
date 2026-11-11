using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.Commands;
using Client.Ui.State.Routing;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.Extension;
using Game.PlacementTarget;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BuildMenu
{
    /// <summary>
    /// コマンドの前提と拒否理由を橋と照合
    /// Checks the build-menu commands' preconditions and reject reasons against the Web bridge
    /// </summary>
    public class BuildMenuCommandsTest
    {
        private GameObject _controlObject;
        private UIStateControl _control;
        private FakeRequester _requester;
        private BuildMenuSelection _selection;
        private PlacementTargetResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _controlObject = new GameObject("BuildMenuCommandsTest.Control");
            _control = _controlObject.AddComponent<UIStateControl>();
            _requester = new FakeRequester();
            _selection = new BuildMenuSelection();
            _resolver = new PlacementTargetResolver(new PlacementTargetCatalog(new BeltConveyorPlacementUnlockSourceMap()), new ClientBlueprintLibrary(), new AllPlacementTargetsUnlocked());
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_controlObject);
        }

        [Test]
        public void ビルドメニュー中の選択は対象を選択へ渡す()
        {
            SetState(UIStateEnum.BuildMenu);
            var entry = _resolver.CreateUnlockedTargets().First();

            var result = CreateCommands(BlueprintDeleteResult.Success).SelectEntry(entry.Id);

            Assert.IsInstanceOf<UiCommandAccepted>(result);
            Assert.IsTrue(_selection.TryConsumeSelectedTarget(out var selected));
            Assert.AreEqual(entry.Id, selected.Id);
        }

        [Test]
        public void 閉じた後と未知のエントリの選択は良性の拒否になる()
        {
            SetState(UIStateEnum.GameScreen);
            var closed = (UiCommandRejected)CreateCommands(BlueprintDeleteResult.Success).SelectEntry(Guid.NewGuid());
            SetState(UIStateEnum.BuildMenu);
            var unknown = (UiCommandRejected)CreateCommands(BlueprintDeleteResult.Success).SelectEntry(Guid.NewGuid());

            Assert.AreEqual("invalid_state", closed.ToWireCode());
            Assert.AreEqual("unknown_entry", unknown.ToWireCode());
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(closed.Reason));
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(unknown.Reason));
            Assert.IsFalse(_selection.TryConsumeSelectedTarget(out _));
        }

        [TestCase(BlueprintDeleteResult.NotFound, "blueprint_delete_not_found", true)]
        [TestCase(BlueprintDeleteResult.NotUnlocked, "blueprint_delete_not_unlocked", false)]
        [TestCase(BlueprintDeleteResult.RequestFailed, "blueprint_delete_request_failed", false)]
        [TestCase(BlueprintDeleteResult.Unknown, "blueprint_delete_unknown", false)]
        public void BP削除の失敗はWebと同じ失敗コードと良性の別に写る(BlueprintDeleteResult deleteResult, string wireCode, bool benign)
        {
            var result = (UiCommandRejected)CreateCommands(deleteResult).DeleteBlueprint(Guid.NewGuid()).GetAwaiter().GetResult();

            Assert.AreEqual(wireCode, result.ToWireCode());
            Assert.AreEqual(benign, UiCommandRejectReasonTraits.IsBenign(result.Reason));
        }

        [Test]
        public void BP削除の成功は受理になる()
        {
            var result = CreateCommands(BlueprintDeleteResult.Success).DeleteBlueprint(Guid.NewGuid()).GetAwaiter().GetResult();

            Assert.IsInstanceOf<UiCommandAccepted>(result);
        }

        [Test]
        public void ビルドメニュー中の閉じるはGameScreenへの遷移を頼んで受理する()
        {
            SetState(UIStateEnum.BuildMenu);

            var result = CreateCommands(BlueprintDeleteResult.Success).Close();

            Assert.IsInstanceOf<UiCommandAccepted>(result);
            CollectionAssert.AreEqual(new[] { UIStateEnum.GameScreen }, _requester.Requested);
        }

        [Test]
        public void 閉じた後の閉じるは遷移を頼まず良性の拒否になる()
        {
            SetState(UIStateEnum.GameScreen);

            var closed = (UiCommandRejected)CreateCommands(BlueprintDeleteResult.Success).Close();

            Assert.IsEmpty(_requester.Requested);
            Assert.AreEqual(UiCommandRejectReason.BuildMenuNotOpen, closed.Reason);
            Assert.AreEqual("invalid_state", closed.ToWireCode());
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(closed.Reason));
        }

        [Test]
        public void 許可表に拒まれた閉じるは非良性の閉じ拒否になる()
        {
            // 開いているのに閉じられないのでトーストで知らせる（BuildMenuNotOpen と取り違えない）
            // Open yet not closable, so it must toast instead of being mistaken for BuildMenuNotOpen
            SetState(UIStateEnum.BuildMenu);
            _requester.Result = false;

            var refused = (UiCommandRejected)CreateCommands(BlueprintDeleteResult.Success).Close();

            CollectionAssert.AreEqual(new[] { UIStateEnum.GameScreen }, _requester.Requested);
            Assert.AreEqual(UiCommandRejectReason.BuildMenuCloseRefused, refused.Reason);
            Assert.AreEqual("transition_not_allowed", refused.ToWireCode());
            Assert.IsFalse(UiCommandRejectReasonTraits.IsBenign(refused.Reason));
        }

        private BuildMenuCommands CreateCommands(BlueprintDeleteResult deleteResult)
        {
            return new BuildMenuCommands(_control, _requester, _resolver, _selection, new BlueprintDeleteServiceStub(deleteResult));
        }

        private void SetState(UIStateEnum state)
        {
            typeof(UIStateControl).GetProperty(nameof(UIStateControl.CurrentState)).SetValue(_control, state);
        }

        private sealed class FakeRequester : IUiStateTransitionRequester
        {
            public readonly List<UIStateEnum> Requested = new();
            public bool Result = true;

            public bool RequestTransition(UIStateEnum requested)
            {
                Requested.Add(requested);
                return Result;
            }
        }

        private sealed class BlueprintDeleteServiceStub : IBlueprintDeleteService
        {
            private readonly BlueprintDeleteResult _result;

            public BlueprintDeleteServiceStub(BlueprintDeleteResult result)
            {
                _result = result;
            }

            public UniTask<BlueprintDeleteResult> DeleteBlueprint(Guid blueprintGuid, CancellationToken ct)
            {
                return UniTask.FromResult(_result);
            }
        }
    }
}
