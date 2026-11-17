using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Toast;
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
        private UiRoutingState _routing;
        private FakeRequester _requester;
        private BuildMenuSelection _selection;
        private PlacementTargetResolver _resolver;
        private RecordingUiCommandResultToaster _toaster;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _controlObject = new GameObject("BuildMenuCommandsTest.Control");
            _routing = UiRoutingStateTestFactory.CreateSwitchable(_controlObject, UIStateEnum.GameScreen, new[] { UIStateEnum.GameScreen, UIStateEnum.BuildMenu }, out _control);
            _requester = new FakeRequester();
            _selection = new BuildMenuSelection();
            _toaster = new RecordingUiCommandResultToaster();
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

            CreateCommands(BlueprintDeleteResult.Success).SelectEntry(entry.Id);

            Assert.AreEqual(BuildMenuCommands.SelectCommandName, _toaster.Reports.Single().CommandName);
            Assert.IsInstanceOf<UiCommandAccepted>(_toaster.Reports.Single().Result);
            Assert.IsTrue(_selection.TryConsumeSelectedTarget(out var selected));
            Assert.AreEqual(entry.Id, selected.Id);
        }

        [Test]
        public void 閉じた後と未知のエントリの選択は良性の拒否になる()
        {
            SetState(UIStateEnum.GameScreen);
            CreateCommands(BlueprintDeleteResult.Success).SelectEntry(Guid.NewGuid());
            SetState(UIStateEnum.BuildMenu);
            CreateCommands(BlueprintDeleteResult.Success).SelectEntry(Guid.NewGuid());
            var closed = (UiCommandRejected)_toaster.Reports[0].Result;
            var unknown = (UiCommandRejected)_toaster.Reports[1].Result;

            Assert.AreEqual("invalid_state", closed.ToWireCode());
            Assert.AreEqual("unknown_entry", unknown.ToWireCode());
            Assert.IsTrue(UiCommandRejectReasonTable.Get(closed.Reason).IsBenign);
            Assert.IsTrue(UiCommandRejectReasonTable.Get(unknown.Reason).IsBenign);
            Assert.IsFalse(_selection.TryConsumeSelectedTarget(out _));
        }

        [TestCase(BlueprintDeleteResult.NotFound, "blueprint_delete_not_found", true)]
        [TestCase(BlueprintDeleteResult.NotUnlocked, "blueprint_delete_not_unlocked", false)]
        [TestCase(BlueprintDeleteResult.RequestFailed, "blueprint_delete_request_failed", false)]
        [TestCase(BlueprintDeleteResult.Unknown, "blueprint_delete_unknown", false)]
        public void BP削除の失敗はWebと同じ失敗コードと良性の別に写る(BlueprintDeleteResult deleteResult, string wireCode, bool benign)
        {
            CreateCommands(deleteResult).DeleteBlueprint(Guid.NewGuid());
            Assert.AreEqual(BuildMenuCommands.DeleteCommandName, _toaster.Reports.Single().CommandName);
            var result = (UiCommandRejected)_toaster.Reports.Single().Result;

            Assert.AreEqual(wireCode, result.ToWireCode());
            Assert.AreEqual(benign, UiCommandRejectReasonTable.Get(result.Reason).IsBenign);
        }

        [Test]
        public void BP削除の成功は受理になり受けたGuidをそのまま削除へ渡す()
        {
            var blueprintGuid = Guid.NewGuid();
            var deleteService = new BlueprintDeleteServiceStub(BlueprintDeleteResult.Success);
            var commands = new BuildMenuCommands(_routing, _requester, _resolver, _selection, deleteService, _toaster);

            commands.DeleteBlueprint(blueprintGuid);

            Assert.IsInstanceOf<UiCommandAccepted>(_toaster.Reports.Single().Result);
            Assert.AreEqual(blueprintGuid, deleteService.LastDeletedGuid);
        }

        [Test]
        public void ビルドメニュー中の閉じるはGameScreenへの遷移を頼んで受理する()
        {
            SetState(UIStateEnum.BuildMenu);

            CreateCommands(BlueprintDeleteResult.Success).Close();

            Assert.AreEqual(UiStateTransitionRules.RequestCommandName, _toaster.Reports.Single().CommandName);
            Assert.IsInstanceOf<UiCommandAccepted>(_toaster.Reports.Single().Result);
            CollectionAssert.AreEqual(new[] { UIStateEnum.GameScreen }, _requester.Requested);
        }

        [Test]
        public void 閉じた後の閉じるは遷移を頼まず良性の拒否になる()
        {
            SetState(UIStateEnum.GameScreen);

            CreateCommands(BlueprintDeleteResult.Success).Close();
            var closed = (UiCommandRejected)_toaster.Reports.Single().Result;

            Assert.IsEmpty(_requester.Requested);
            Assert.AreEqual(UiCommandRejectReason.BuildMenuNotOpen, closed.Reason);
            Assert.AreEqual("invalid_state", closed.ToWireCode());
            Assert.IsTrue(UiCommandRejectReasonTable.Get(closed.Reason).IsBenign);
        }

        [Test]
        public void 許可表に拒まれた閉じるは非良性の閉じ拒否になる()
        {
            // 開いているのに閉じられないのでトーストで知らせる（BuildMenuNotOpen と取り違えない）
            // Open yet not closable, so it must toast instead of being mistaken for BuildMenuNotOpen
            SetState(UIStateEnum.BuildMenu);
            _requester.Result = false;

            CreateCommands(BlueprintDeleteResult.Success).Close();
            var refused = (UiCommandRejected)_toaster.Reports.Single().Result;

            CollectionAssert.AreEqual(new[] { UIStateEnum.GameScreen }, _requester.Requested);
            Assert.AreEqual(UiCommandRejectReason.ScreenCloseRefused, refused.Reason);
            Assert.AreEqual("transition_not_allowed", refused.ToWireCode());
            Assert.IsFalse(UiCommandRejectReasonTable.Get(refused.Reason).IsBenign);
        }

        private BuildMenuCommands CreateCommands(BlueprintDeleteResult deleteResult)
        {
            return new BuildMenuCommands(_routing, _requester, _resolver, _selection, new BlueprintDeleteServiceStub(deleteResult), _toaster);
        }

        // 実際の遷移で UiRoutingState まで写す（コマンドは画面の開閉を routing で読む。A-248）
        // Transit for real so the change reaches UiRoutingState (the commands read open/closed through routing; A-248)
        private void SetState(UIStateEnum state)
        {
            UiRoutingStateTestFactory.Transit(_control, state);
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
            public Guid LastDeletedGuid { get; private set; }

            public BlueprintDeleteServiceStub(BlueprintDeleteResult result)
            {
                _result = result;
            }

            public UniTask<BlueprintDeleteResult> DeleteBlueprint(Guid blueprintGuid, CancellationToken ct)
            {
                LastDeletedGuid = blueprintGuid;
                return UniTask.FromResult(_result);
            }
        }
    }
}
