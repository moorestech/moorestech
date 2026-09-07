using System;
using ClassLibrary;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Core;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
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
    ///     アクション系APIはオーバーレイ表示と0.5秒インターバルを自動で伴う（動画の可読性のため）
    ///     Action-style APIs automatically show an overlay step and insert a 0.5s interval (for recording readability)
    /// </summary>
    public class PlaytestDriver
    {
        // 開幕スキットのSkip受理待ちと、その後のGameScreen到達待ちの上限
        // Timeouts for the opening skit's skip acceptance and the subsequent arrival at GameScreen
        private const float SkitSkipTimeoutSeconds = 30f;
        private const float SkitSkipUiStateTimeoutSeconds = 15f;
        // 再生されないワールドでも待ちすぎないための、任意スキップの上限
        // Upper bound for the optional skip so worlds without a skit do not stall
        private const float OptionalSkitSkipTimeoutSeconds = 10f;

        private readonly PlaytestResult _result;
        private readonly string _runDirectory;
        private readonly PlaytestReporter _reporter;
        // ホットバー操作のサブファサード
        // Sub-facade for hotbar operations (EnterBuildMode/ExitBuildMode/AssignHotbar/UnlockConnectTool)
        public PlaytestHotbarDriver Hotbar { get; }
        public PlaytestDriver(PlaytestResult result, string runDirectory)
        {
            _result = result;
            _runDirectory = runDirectory;
            _reporter = new PlaytestReporter(result);
            Hotbar = new PlaytestHotbarDriver(_reporter);
        }
        // AIナレーション用。今から何をするか・何が起きたかを動画とTimelineに残す
        // For AI narration: records what's about to happen / what happened, into the video and Timeline
        public void Note(string message) => _reporter.Note(message);
        // ---- セットアップ / Setup ----
        public async UniTask SetupFlatGround() => await _reporter.Act("地形を平坦化してプレイヤーをワープ", PlaytestSetup.CreateFlatGroundAndWarp);
        public async UniTask SetupDebugEnvironment(PlaytestEnvironmentConfig config)
        {
            // 設定変更前に全値を動画オーバーレイとTimelineへ残す
            // Preserve every value in the video overlay and Timeline before changing settings
            var summary = PlaytestSetup.FormatEnvironmentConfig(config);
            Debug.Log(summary);
            Note(summary);
            await _reporter.Act("デバッグ環境をセットアップ", () => PlaytestSetup.SetupDebugEnvironment(config));
        }
        public void WarpPlayer(Vector3 position)
        {
            _reporter.Step($"ワープ: {position}");
            PlaytestSetup.WarpPlayer(position);
        }
        public Vector3 PlayerPosition => PlayerSystemContainer.Instance.PlayerObjectController.Position;
        // ---- アイテム / Items ----
        public void GiveItemDirect(string itemName, int count)
        {
            _reporter.Step($"give(direct): {itemName} x{count}");
            PlaytestItemOps.GiveItemDirect(itemName, count);
        }
        public async UniTask GiveItem(string itemName, int count) => await _reporter.Act($"give: {itemName} x{count}", () => PlaytestItemOps.GiveItemViaCommand(itemName, count, 10f));
        public int CountItem(string itemName) => PlaytestItemOps.CountItem(ClientContext.PlayerConnectionSetting.PlayerId, PlaytestItemOps.ResolveItemId(itemName));
        // slotは0始まりの装備枠番号。入れると同時にその枠を選択する（採掘はサーバー権威で選択中装備を見るため）
        // slot is the zero-based equipment slot; equipping also selects it (server-authoritative mining reads the selected equipment)
        public async UniTask EquipItem(string itemName, int equipmentSlot) => await _reporter.Act($"装備{equipmentSlot + 1}へ装着: {itemName}", () => PlaytestItemOps.EquipItem(itemName, equipmentSlot, 15f));
        // ---- ブロック / Blocks ----
        public IBlock PlaceBlockDirect(string blockName, Vector3Int position, BlockDirection direction)
        {
            _reporter.Step($"設置(direct): {blockName} at {position}");
            return PlaytestBlockOps.PlaceBlockDirect(blockName, position, direction);
        }
        public bool RemoveBlock(Vector3Int position)
        {
            _reporter.Step($"撤去: {position}");
            return PlaytestBlockOps.RemoveBlock(position);
        }
        public IBlock GetBlock(Vector3Int position) => PlaytestBlockOps.GetBlock(position);
        public async UniTask<BlockGameObject> WaitBlockGameObject(Vector3Int position)
        {
            var waitEntry = _reporter.BeginWait($"ブロック生成待ち: {position}");
            var blockGameObject = await PlaytestBlockOps.WaitBlockGameObjectSpawn(position, 15f);
            _reporter.EndWait(waitEntry);
            return blockGameObject;
        }
        // ---- UI経路操作 / UI-route operations ----
        public UIStateEnum CurrentUiState => PlaytestUiOps.CurrentUiState();
        public void UnlockBlock(string blockName)
        {
            _reporter.Step($"アンロック: {blockName}");
            PlaytestBlockOps.UnlockBlockServerSide(blockName);
        }
        public async UniTask GiveConstructionCost(string blockName, int blockCount) => await _reporter.Act($"建設コスト付与: {blockName} x{blockCount}", () => PlaytestItemOps.GiveConstructionCost(blockName, blockCount, 15f));
        public async UniTask PrepareBlockForUiPlacement(string blockName, int blockCount)
        {
            // UI設置の前提を1行で整える: アンロック＋建設コスト付与（クライアント在庫反映待ち込み）
            // One-liner setup for UI placement: unlock plus construction-cost grant (waits for client inventory sync)
            UnlockBlock(blockName);
            await GiveConstructionCost(blockName, blockCount);
        }
        public async UniTask PressKey(Key key) => await _reporter.Act($"キー入力: {key}", () => SemanticInput.TapKey(key));
        public async UniTask ClickWebUi(string testid) => await _reporter.Act($"Web UIクリック: {testid}", () => PlaytestWebUiOps.ClickWebUi(testid));
        public async UniTask HoverWebUi(string testid) => await _reporter.Act($"Web UIホバー: {testid}", () => PlaytestWebUiOps.HoverWebUi(testid));
        public async UniTask UntilWebUiElement(string testid, float timeoutSeconds)
        {
            // DOM要素待ちを待機表示し、成功後も録画用の操作間隔を確保する
            // Show DOM waiting and retain the recording pace after success
            var waitEntry = _reporter.BeginWait($"Web UI要素待ち: {testid}");
            await PlaytestWebUiOps.WaitWebUiElement(testid, timeoutSeconds);
            _reporter.EndWait(waitEntry);
            await UniTask.Delay(TimeSpan.FromSeconds(PlaytestReporter.ActionIntervalSeconds), ignoreTimeScale: true);
        }
        public async UniTask ClickBuildMenuBlock(string blockName) => await ClickWebUi(PlaytestWebUiOps.BuildMenuBlockTestId(blockName));
        public async UniTask CloseWebUiPanel() => await ClickWebUi("build-menu-close");
        public async UniTask WaitUiState(UIStateEnum state, float timeoutSeconds)
        {
            var waitEntry = _reporter.BeginWait($"UI状態待ち: {state}");
            await PlaytestUiOps.WaitUiState(state, timeoutSeconds);
            _reporter.EndWait(waitEntry);
        }
        public async UniTask OpenBuildMenuAndSelectBlock(string blockName) => await _reporter.Act($"ビルドメニューで選択: {blockName}", () => PlaytestBuildMenuOps.OpenBuildMenuAndSelectBlock(blockName));
        // 開幕スキットを飛ばしゲーム画面まで抜ける。ホットバー・ビルドメニュー操作すべての前提
        // Skips the opening skit and lands on the game screen; the precondition for every hotbar and build-menu operation
        public async UniTask SkipOpeningSkit() => await _reporter.Act("開幕スキットをSkipインテントで飛ばす", () => PlaytestSkitOps.SkipOpeningSkit(SkitSkipTimeoutSeconds, SkitSkipUiStateTimeoutSeconds));
        // スキットが再生されないワールドでも落ちない版。再生されていれば飛ばし、されていなければそのまま進む
        // Variant that tolerates worlds without an opening skit: skips it when it plays, moves on when it does not
        public async UniTask SkipOpeningSkitIfPlaying() => await _reporter.Act("開幕スキットを再生中なら飛ばす", () => PlaytestSkitOps.SkipOpeningSkitIfPlaying(OptionalSkitSkipTimeoutSeconds));
        public async UniTask ExitToGameScreen() => await _reporter.Act("ゲーム画面へ戻る", PlaytestUiOps.ExitToGameScreen);
        public async UniTask AimAt(Vector3 worldPosition) => await _reporter.Act($"照準: {worldPosition}", () => PlaytestUiOps.AimAtWorldPosition(worldPosition));
        // 指定originに設置されるよう接地面上のフットプリント中心へ照準する（向きはNorth前提）
        // Aim at the footprint center on the ground so placement lands on the given origin (assumes North)
        public async UniTask AimAtPlaceOrigin(string blockName, Vector3Int origin) => await _reporter.Act($"照準: {blockName} at {origin}", () => PlaytestUiOps.AimAtWorldPosition(PlaytestUiOps.PlaceAimPoint(blockName, origin, BlockDirection.North)));
        public async UniTask ClickPlace() => await _reporter.Act("クリック設置", PlaytestUiOps.ClickPlace);
        public async UniTask PlaceBlockViaUi(string blockName, Vector3Int origin, BlockDirection direction)
        {
            // ビルドメニュー選択→照準→クリック設置→サーバー反映待ちの統合操作（方向はデフォルトNorth前提）
            // Composite op: build-menu select, aim, click-place, then wait for the server-side block (direction assumes default North)
            await OpenBuildMenuAndSelectBlock(blockName);
            await _reporter.Act($"照準: {blockName} at {origin}", () => PlaytestUiOps.AimAtWorldPosition(PlaytestUiOps.PlaceAimPoint(blockName, origin, direction)));
            await ClickPlace();
            await Until(() => PlaytestBlockOps.GetBlock(origin) != null, 15f, $"UI設置反映: {blockName} at {origin}");
        }
        public async UniTask DragPlaceViaUi(string blockName, Vector3Int fromOrigin, Vector3Int toOrigin)
        {
            // ドラッグ設置（ベルト等の経路設置）。方向はドラッグ経路から自動解決される
            // Drag placement (belt runs, etc.); direction is auto-resolved from the drag path
            await OpenBuildMenuAndSelectBlock(blockName);
            var fromAim = PlaytestUiOps.PlaceAimPoint(blockName, fromOrigin, BlockDirection.North);
            var toAim = PlaytestUiOps.PlaceAimPoint(blockName, toOrigin, BlockDirection.North);
            _reporter.Step($"ドラッグ設置: {blockName} {fromOrigin}->{toOrigin}");
            // ドラッグ直後に0.5秒アイドルさせると、続く連続ドラッグの始点タイルが設置されない実バグがあるため、
            // このアクションだけは通常のActではなくインターバル無しで実行する（既存ゲームロジックの制約）
            // Idling 0.5s right after a drag corrupts the next consecutive drag's start-cell placement (a real
            // in-game bug), so skip the usual Act interval for this action only (constraint of existing game logic)
            await PlaytestUiOps.DragPlace(fromAim, toAim);
            await Until(() => PlaytestBlockOps.GetBlock(fromOrigin) != null && PlaytestBlockOps.GetBlock(toOrigin) != null, 15f, $"UIドラッグ設置反映: {blockName} {fromOrigin}->{toOrigin}");
        }
        public void SendCommand(string command)
        {
            _reporter.Step($"コマンド送信: {command}");
            ClientContext.VanillaApi.SendOnly.SendCommand(command);
        }
        public TService ServerService<TService>() => ServerContext.GetService<TService>();
        // ---- 検証 / Verification ----
        public void Assert(bool condition, string label)
        {
            // 成否を結果JSONへ記録する（失敗しても実行は継続する）
            // Record pass/fail into the result JSON (execution continues on failure)
            _reporter.RecordAssert(condition, label, condition ? "ok" : "assertion failed");
            if (!condition) Debug.LogWarning($"[Playtest] assert failed: {label}");
        }
        public async UniTask Until(Func<bool> condition, float timeoutSeconds, string label)
        {
            // 固定sleepの代替。条件成立をフレームポーリングで待ち、結果も記録する
            // Replacement for fixed sleeps: poll per frame until the condition holds, recording the outcome
            var waitEntry = _reporter.BeginWait(label);
            var startTime = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (timeoutSeconds < Time.realtimeSinceStartup - startTime)
                {
                    _reporter.RecordUntilResult(false, label, $"Until timeout after {timeoutSeconds}s");
                    throw new TimeoutException($"Until '{label}' timed out after {timeoutSeconds}s");
                }
                await UniTask.Yield();
            }
            _reporter.EndWait(waitEntry);
            _reporter.RecordUntilResult(true, label, "condition met");
        }
        public async UniTask WaitSeconds(float seconds)
        {
            _reporter.BeginWait($"{seconds:0.#}秒");
            await UniTask.Delay(TimeSpan.FromSeconds(seconds));
        }
        public async UniTask<string> Screenshot(string name)
        {
            _reporter.Step($"スクリーンショット: {name}");
            var path = await PlaytestScreenshot.Capture(_runDirectory, name);
            _result.Screenshots.Add(path);
            return path;
        }
    }
}
