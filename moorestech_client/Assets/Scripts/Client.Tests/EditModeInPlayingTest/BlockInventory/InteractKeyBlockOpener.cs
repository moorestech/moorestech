using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    /// <summary>
    ///     プレイヤーの手の届く所にブロックを置き、Fキーの入力注入で本番のUI遷移を通して開く
    ///     Places a block within the player's reach and opens it through the production UI transition by injecting the F key
    /// </summary>
    public static class InteractKeyBlockOpener
    {
        // 照準が外れても近傍2m内の候補として拾われる距離に置く
        // Close enough that the 2m nearby fallback picks the block even if the aim ray misses it
        private const float BlockCenterDistanceFromPlayer = 2f;

        public static async UniTask<BlockGameObject> PlaceNearPlayerAndOpen(string blockName)
        {
            var resolver = ClientDIContext.DIContainer.DIContainerResolver;
            var uiStateControl = resolver.Resolve<UIStateControl>();
            var subInventoryState = resolver.Resolve<SubInventoryState>();
            await WaitUiState(uiStateControl, UIStateEnum.GameScreen);

            // テストワールドには地面が無いため、録画と同じ足場へプレイヤーを立たせる
            // The test world has no ground, so stand the player on the same scaffold the recordings use
            await PlaytestSetup.CreateFlatGroundAndWarp();

            // 視線の水平前方にブロックの中心が来るよう原点を決めて置く
            // Pick the origin so the block's center sits ahead of the player along the horizontal view direction
            var pos = OriginAheadOfPlayer();
            PlaceBlock(blockName, pos, BlockDirection.North);
            var blockGameObject = await WaitBlockGameObjectSpawn(pos);

            // Editorが背面だとキー入力が捨てられるため、録画シナリオと同じく押す間だけ背景でも届く設定にする
            // A background Editor drops key input, so like the recorded scenarios let input through only while pressing
            var inputSettings = InputSystem.settings;
            var (savedBackground, savedEditorInput) = (inputSettings.backgroundBehavior, inputSettings.editorInputBehaviorInPlayMode);
            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            // ゲーム画面のインタラクトと同じ入力経路でFを押し、SubInventoryへの遷移を待つ
            // Press F through the same input path as the game screen's interact and wait for the SubInventory transition
            SemanticInput.EnsureDevices();
            var keyboardWasEnabled = Keyboard.current.enabled;
            InputSystem.EnableDevice(Keyboard.current);
            await SemanticInput.TapKey(Key.F);
            for (var i = 0; i < 300 && uiStateControl.CurrentState != UIStateEnum.SubInventory; i++) await UniTask.Yield();
            if (!keyboardWasEnabled) InputSystem.DisableDevice(Keyboard.current);
            inputSettings.backgroundBehavior = savedBackground;
            inputSettings.editorInputBehaviorInPlayMode = savedEditorInput;
            await WaitUiState(uiStateControl, UIStateEnum.SubInventory);

            // 開いた対象が置いたブロックで、インベントリの取得まで済んでいる
            // The opened source is the placed block and its inventory fetch has completed
            var source = subInventoryState.CurrentSubInventorySource as BlockSubInventorySource;
            Assert.IsNotNull(source, "SubInventory was opened for a non-block source");
            Assert.AreEqual(blockGameObject.BlockPosInfo.OriginalPos, source.BlockPosition, "F opened a different block");
            for (var i = 0; i < 300 && subInventoryState.CurrentSubInventory == null; i++) await UniTask.Yield();
            Assert.IsNotNull(subInventoryState.CurrentSubInventory, "sub inventory did not load");
            return blockGameObject;

            #region Internal

            Vector3Int OriginAheadOfPlayer()
            {
                var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
                var blockSize = (Vector3)MasterHolder.BlockMaster.GetBlockMaster(FindBlockIdByName(blockName)).BlockSize;
                var center = playerPosition + forward * BlockCenterDistanceFromPlayer;
                return Vector3Int.FloorToInt(new Vector3(center.x - blockSize.x / 2f, playerPosition.y, center.z - blockSize.z / 2f));
            }

            #endregion
        }

        // 開いているUIを本番のWeb UIと同じ遷移要求で閉じる
        // Close the open UI with the same transition request the production Web UI uses
        public static async UniTask CloseToGameScreen()
        {
            var uiStateControl = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
            uiStateControl.RequestTransition(UIStateEnum.GameScreen);
            await WaitUiState(uiStateControl, UIStateEnum.GameScreen);
        }

        private static async UniTask WaitUiState(UIStateControl uiStateControl, UIStateEnum state)
        {
            for (var i = 0; i < 300 && uiStateControl.CurrentState != state; i++) await UniTask.Yield();
            Assert.AreEqual(state, uiStateControl.CurrentState, $"UI state did not become {state}");
        }
    }
}
