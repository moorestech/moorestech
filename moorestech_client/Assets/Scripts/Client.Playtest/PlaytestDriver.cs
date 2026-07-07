using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Core;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Playtest
{
    /// <summary>
    ///     シナリオへ渡されるセマンティックAPIの窓口。1行=1意味操作でシナリオを記述できるようにする
    ///     Semantic API facade handed to scenarios, enabling one-line-per-operation scenario scripts
    /// </summary>
    public class PlaytestDriver
    {
        private readonly PlaytestResult _result;
        private readonly string _runDirectory;

        public PlaytestDriver(PlaytestResult result, string runDirectory)
        {
            _result = result;
            _runDirectory = runDirectory;
        }

        // ---- セットアップ / Setup ----
        public async UniTask SetupFlatGround() => await PlaytestSetup.CreateFlatGroundAndWarp();
        public void WarpPlayer(Vector3 position) => PlaytestSetup.WarpPlayer(position);
        public Vector3 PlayerPosition => PlayerSystemContainer.Instance.PlayerObjectController.Position;

        // ---- アイテム / Items ----
        public void GiveItemDirect(string itemName, int count) => PlaytestItemOps.GiveItemDirect(itemName, count);
        public async UniTask GiveItem(string itemName, int count) => await PlaytestItemOps.GiveItemViaCommand(itemName, count, 10f);
        public int CountItem(string itemName) => PlaytestItemOps.CountItem(ClientContext.PlayerConnectionSetting.PlayerId, PlaytestItemOps.ResolveItemId(itemName));

        // ---- ブロック / Blocks ----
        public IBlock PlaceBlockDirect(string blockName, Vector3Int position, BlockDirection direction) => PlaytestBlockOps.PlaceBlockDirect(blockName, position, direction);
        public bool RemoveBlock(Vector3Int position) => PlaytestBlockOps.RemoveBlock(position);
        public IBlock GetBlock(Vector3Int position) => PlaytestBlockOps.GetBlock(position);
        public async UniTask<BlockGameObject> WaitBlockGameObject(Vector3Int position) => await PlaytestBlockOps.WaitBlockGameObjectSpawn(position, 15f);

        // ---- UI経路操作 / UI-route operations ----
        public UIStateEnum CurrentUiState => PlaytestUiOps.CurrentUiState();
        public void UnlockBlock(string blockName) => PlaytestBlockOps.UnlockBlockServerSide(blockName);
        public async UniTask PressKey(Key key) => await SemanticInput.TapKey(key);
        // slotは0始まり（HotBarView.SelectIndexと同じ）。0→キー1、8→キー9
        // slot is zero-based (same as HotBarView.SelectIndex): 0 -> key "1", 8 -> key "9"
        public async UniTask SelectHotbar(int slot) => await SemanticInput.TapKey(Key.Digit1 + slot);
        public async UniTask WaitUiState(UIStateEnum state, float timeoutSeconds) => await PlaytestUiOps.WaitUiState(state, timeoutSeconds);
        public async UniTask OpenBuildMenuAndSelectBlock(string blockName) => await PlaytestUiOps.OpenBuildMenuAndSelectBlock(blockName);
        public async UniTask ExitToGameScreen() => await PlaytestUiOps.ExitToGameScreen();
        public async UniTask AimAt(Vector3 worldPosition) => await PlaytestUiOps.AimAtWorldPosition(worldPosition);
        public async UniTask ClickPlace() => await PlaytestUiOps.ClickPlace();

        public async UniTask PlaceBlockViaUi(string blockName, Vector3Int origin, BlockDirection direction)
        {
            // ビルドメニュー選択→照準→クリック設置→サーバー反映待ちの統合操作（方向はデフォルトNorth前提）
            // Composite op: build-menu select, aim, click-place, then wait for the server-side block (direction assumes default North)
            await PlaytestUiOps.OpenBuildMenuAndSelectBlock(blockName);
            await PlaytestUiOps.AimAtWorldPosition(PlaytestUiOps.PlaceAimPoint(blockName, origin, direction));
            await PlaytestUiOps.ClickPlace();
            await Until(() => PlaytestBlockOps.GetBlock(origin) != null, 15f, $"UI設置反映: {blockName} at {origin}");
        }

        public async UniTask DragPlaceViaUi(string blockName, Vector3Int fromOrigin, Vector3Int toOrigin)
        {
            // ドラッグ設置（ベルト等の経路設置）。方向はドラッグ経路から自動解決される
            // Drag placement (belt runs, etc.); direction is auto-resolved from the drag path
            await PlaytestUiOps.OpenBuildMenuAndSelectBlock(blockName);
            var fromAim = PlaytestUiOps.PlaceAimPoint(blockName, fromOrigin, BlockDirection.North);
            var toAim = PlaytestUiOps.PlaceAimPoint(blockName, toOrigin, BlockDirection.North);
            await PlaytestUiOps.DragPlace(fromAim, toAim);
            await Until(() => PlaytestBlockOps.GetBlock(fromOrigin) != null && PlaytestBlockOps.GetBlock(toOrigin) != null, 15f, $"UIドラッグ設置反映: {blockName} {fromOrigin}->{toOrigin}");
        }

        // ---- 低レベルアクセス / Low-level access ----
        public void SendCommand(string command) => ClientContext.VanillaApi.SendOnly.SendCommand(command);
        public TService ServerService<TService>() => ServerContext.GetService<TService>();

        // ---- 検証 / Verification ----
        public void Assert(bool condition, string label)
        {
            // 成否を結果JSONへ記録する（失敗しても実行は継続する）
            // Record pass/fail into the result JSON (execution continues on failure)
            _result.Asserts.Add(new PlaytestAssertResult { Label = label, Passed = condition, Message = condition ? "ok" : "assertion failed" });
            if (!condition) Debug.LogWarning($"[Playtest] assert failed: {label}");
        }

        public async UniTask Until(Func<bool> condition, float timeoutSeconds, string label)
        {
            // 固定sleepの代替。条件成立をフレームポーリングで待ち、結果も記録する
            // Replacement for fixed sleeps: poll per frame until the condition holds, recording the outcome
            var startTime = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - startTime > timeoutSeconds)
                {
                    _result.Asserts.Add(new PlaytestAssertResult { Label = label, Passed = false, Message = $"Until timeout after {timeoutSeconds}s" });
                    throw new TimeoutException($"Until '{label}' timed out after {timeoutSeconds}s");
                }
                await UniTask.Yield();
            }
            _result.Asserts.Add(new PlaytestAssertResult { Label = label, Passed = true, Message = "condition met" });
        }

        public async UniTask WaitSeconds(float seconds) => await UniTask.Delay(TimeSpan.FromSeconds(seconds));

        public async UniTask<string> Screenshot(string name)
        {
            var path = await PlaytestScreenshot.Capture(_runDirectory, name);
            _result.Screenshots.Add(path);
            return path;
        }
    }
}
