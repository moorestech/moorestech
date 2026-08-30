using System.Runtime.Serialization;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.Skit;
using Client.Tests.Map.Vein;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     右短押しでUI・建築/破壊モードを閉じる配線をステート単位で検証。ManualUpdate等の内部詳細ではなく、実際のPress→Release操作からの遷移結果だけを見る
    ///     Verifies the right-short-press-closes-UI wiring per state at the level of an actual Press-to-Release operation and its resulting transition, not internal details such as ManualUpdate
    /// </summary>
    public class RightShortPressTransitionTest : UIStateTestFixtureBase
    {
        [Test]
        public void PlaceBlock進行中操作があれば右短押しで解除だけ行い建築モードに留まる()
        {
            var placeSystem = new CancellablePlaceSystem { CancelResult = true };
            var state = CreatePlaceBlockState(new SinglePlaceSystemSelector(placeSystem));
            state.OnEnter(new UITransitContext(UIStateEnum.PlaceBlock));

            var transit = PressAndReleaseRightButton(state);

            Assert.IsNull(transit, "進行中操作の解除だけなので建築モードへ留まる");
            Assert.AreEqual(1, placeSystem.CancelCallCount);
        }

        [Test]
        public void PlaceBlock進行中操作が無ければ右短押しで建築モードを抜ける()
        {
            var placeSystem = new CancellablePlaceSystem { CancelResult = false };
            var state = CreatePlaceBlockState(new SinglePlaceSystemSelector(placeSystem));
            state.OnEnter(new UITransitContext(UIStateEnum.PlaceBlock));

            var transit = PressAndReleaseRightButton(state);

            Assert.AreEqual(UIStateEnum.GameScreen, transit?.NextStateEnum);
        }

        [Test]
        public void DeleteObject削除選択が無ければ右短押しで破壊モードを抜ける()
        {
            SetUpMouseCursorTooltip();
            var deleteBarObject = CreateComponent<DeleteBarObject>("DeleteBar");
            var rightShortPressInputService = new RightShortPressInputService(new RightShortPressInput());
            var state = new DeleteObjectState(deleteBarObject, null, CreateCameraPolicy(new FakePlayerCameraInteractionApplier()), new BuildOperationHistory(), new BuildUndoService(new BuildOperationHistory(), null), new PlacementTargetPickService(null), rightShortPressInputService);
            state.OnEnter(new UITransitContext(UIStateEnum.DeleteBar));

            var transit = PressAndReleaseRightButton(state);

            Assert.AreEqual(UIStateEnum.GameScreen, transit?.NextStateEnum);
        }

        [Test]
        public void BuildMenu右短押しでゲーム画面へ抜ける()
        {
            var rightShortPressInputService = new RightShortPressInputService(new RightShortPressInput());
            var state = new BuildMenuState(new FakeBuildMenuView(), CreateCameraPolicy(new FakePlayerCameraInteractionApplier()), rightShortPressInputService);
            state.OnEnter(new UITransitContext(UIStateEnum.BuildMenu));

            var transit = PressAndReleaseRightButton(state);

            Assert.AreEqual(UIStateEnum.GameScreen, transit?.NextStateEnum);
        }

        [Test]
        public void ChallengeList右短押しでゲーム画面へ抜ける()
        {
            var challengeListView = CreateComponent<ChallengeListView>("ChallengeList");
            var rightShortPressInputService = new RightShortPressInputService(new RightShortPressInput());
            var state = new ChallengeListState(challengeListView, rightShortPressInputService);
            state.OnEnter(new UITransitContext(UIStateEnum.ChallengeList));

            var transit = PressAndReleaseRightButton(state);

            Assert.AreEqual(UIStateEnum.GameScreen, transit?.NextStateEnum);
        }

        // 8px未満で押して離すだけの短押しをシミュレートする（移動なし）
        // Simulates a short press below the 8px threshold (press then release with no movement)
        private UITransitContext PressAndReleaseRightButton(IUIState state)
        {
            Press(MouseDevice.rightButton);
            state.GetNextUpdate();
            Release(MouseDevice.rightButton);
            return state.GetNextUpdate();
        }

        private PlaceBlockState CreatePlaceBlockState(IPlaceSystemSelector selector)
        {
            var skitManager = (SkitManager)FormatterServices.GetUninitializedObject(typeof(SkitManager));
            var dataStore = CreateComponent<BlockGameObjectDataStore>("BlockDataStore");
            var placeStateController = new PlaceSystemStateController(selector, new PlacementFeedbackTooltipPresenter());
            var pickService = new PlacementTargetPickService(null);
            var hotbarInputService = CreateHotbarTapInputService(placeStateController);
            var rightShortPressInputService = new RightShortPressInputService(new RightShortPressInput());
            return new PlaceBlockState(skitManager, dataStore, placeStateController, pickService, CreateCameraPolicy(new FakePlayerCameraInteractionApplier()), new BuildUndoService(new BuildOperationHistory(), dataStore), new FakeMapVeinRangeView(), MapVeinAabbRegistryFixture.Create(), new VeinRestrictedPlacementState(), hotbarInputService, rightShortPressInputService);
        }

        // 選択にかかわらず常に同じ設置系を返すテスト用セレクタ
        // Test selector that always returns the same place system regardless of the current selection
        private class SinglePlaceSystemSelector : IPlaceSystemSelector
        {
            private readonly IPlaceSystem _placeSystem;

            public SinglePlaceSystemSelector(IPlaceSystem placeSystem)
            {
                _placeSystem = placeSystem;
            }

            public IPlaceSystem EmptyPlaceSystem { get; } = new Client.Game.InGame.BlockSystem.PlaceSystem.Empty.EmptyPlaceSystem();

            public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context)
            {
                return _placeSystem;
            }
        }

        // 解除呼び出しの有無・結果を記録するテスト用設置系
        // Test place system recording whether and how a cancel was invoked
        private class CancellablePlaceSystem : IPlaceSystem
        {
            public bool CancelResult;
            public int CancelCallCount;
            public bool OwnsWheelInput => false;
            public void Enable() { }
            public void ManualUpdate(PlaceSystemUpdateContext context) { }
            public void Disable() { }

            public bool TryCancelInProgressOperation()
            {
                CancelCallCount++;
                return CancelResult;
            }
        }
    }
}
