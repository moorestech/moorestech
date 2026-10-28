using System.Collections.Generic;
using System.Linq;
using Client.DebugSystem.Environment;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

// 録画前に単体キー入力とデバイスを実確認する
// Probe one key action and devices before recording
var viewFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var activeGameView = Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == "GameView");
var dock = typeof(UnityEditor.EditorWindow).GetField("m_Parent", viewFlags).GetValue(activeGameView);
var tabs = (System.Collections.IList)dock.GetType().GetField("m_Panes", viewFlags).GetValue(dock);
dock.GetType().GetProperty("selected", viewFlags).SetValue(dock, tabs.IndexOf(activeGameView));
activeGameView.Focus();
await UniTask.DelayFrame(3);
Debug.Log($"[INPUT] focus={Application.isFocused} background={InputSystem.settings.backgroundBehavior} keyboard={Keyboard.current.enabled} mouse={Mouse.current.enabled}");
if (!Keyboard.current.enabled || !Mouse.current.enabled) throw new System.InvalidOperationException("Input devices disabled");
await SemanticInput.TapKey(Key.Escape);
await UniTask.WaitUntil(() => PlaytestUiOps.CurrentUiState() == UIStateEnum.PauseMenu).Timeout(System.TimeSpan.FromSeconds(15));
await SemanticInput.TapKey(Key.Escape);
await UniTask.WaitUntil(() => PlaytestUiOps.CurrentUiState() == UIStateEnum.GameScreen).Timeout(System.TimeSpan.FromSeconds(15));
var hadPreference = PlayerPrefs.HasKey("GraphicsQuality");
var savedPreference = PlayerPrefs.GetString("GraphicsQuality");
var savedSettings = GraphicsQualityPreference.Current;
var savedRecovered = GraphicsQualityPreference.RecoveredInvalidPreference;
return PlaytestRunner.Run("quality-fps-measure", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 240f }, async p =>
{
    System.IDisposable sampling = null;
    try
    {
        // 通常再生と同じ環境・実セーブの密集帯を使う
        // Use the normal environment and the dense area of the real save
        Common.Debug.DebugParameters.SaveInt("DebugEnvironmentTypeKey", 0);
        DebugEnvironmentController.SetEnvironment(DebugEnvironmentType.Debug);
        await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { CreateFlatGround = false, SpawnPosition = new Vector3(659.5f, 96.6f, 586.5f) });
        await p.SkipOpeningSkitIfPlaying();
        GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Medium);
        var data = ClientDIContext.DIContainer.DIContainerResolver.Resolve<BlockGameObjectDataStore>();
        var blockCount = await p.ReadWorldBlockCount();
        await p.Until(() => data.BlockGameObjectDictionary.Count >= blockCount, 90f, "実セーブ全ブロックのView受信");
        p.Assert(Camera.main.GetComponent<Cinemachine.CinemachineBrain>().enabled, "CinemachineBrainを止めず通常カメラ追従");
        p.Assert(blockCount == 13852, $"原本の13852ブロック: {blockCount}");
        p.Assert(GraphicsQualityPreference.Current.Equals(GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium)), "中プリセットで計測");
        await p.WaitSeconds(10f);
        await p.Screenshot("01-before-measurement");

        // 実時間60秒の全フレームと実移動距離を採る
        // Sample all frames and actual walking distance for sixty real seconds
        var dts = new List<float>();
        var distance = 0f;
        var focusedFrames = 0;
        var lastPosition = p.PlayerPosition;
        var started = Time.realtimeSinceStartupAsDouble;
        sampling = Observable.EveryEndOfFrame().Subscribe(_ =>
        {
            dts.Add(Time.unscaledDeltaTime);
            if (Application.isFocused) focusedFrames++;
            var position = p.PlayerPosition;
            distance += Vector2.Distance(new Vector2(position.x, position.z), new Vector2(lastPosition.x, lastPosition.z));
            lastPosition = position;
        });
        p.Note("中・録画あり・カメラ通常追従で60秒、歩行と新規設置を行う");
        var placed = false;
        var origin = Vector3Int.zero;
        var nextKey = Key.W;
        while (Time.realtimeSinceStartupAsDouble - started < 60d)
        {
            SemanticInput.KeyDown(nextKey);
            var until = System.Math.Min(started + 60d, Time.realtimeSinceStartupAsDouble + 3d);
            await p.Until(() => Time.realtimeSinceStartupAsDouble >= until, 6f, $"歩行 {nextKey}");
            SemanticInput.KeyUp(nextKey);
            await p.WaitSeconds(0.5f);
            nextKey = nextKey == Key.W ? Key.S : Key.W;
            if (!placed && Time.realtimeSinceStartupAsDouble - started >= 24d)
            {
                // 原本の地形を変えず、本番UIから床上へ設置する
                // Place on the existing floor through the production UI without altering terrain
                origin = new Vector3Int(Mathf.FloorToInt(p.PlayerPosition.x), 96, Mathf.FloorToInt(p.PlayerPosition.z) + 4);
                p.Assert(p.GetBlock(origin) == null, "設置先は空セル");
                await p.PlaceBlockViaUi("木のチェスト", origin, BlockDirection.North);
                var view = await p.WaitBlockGameObject(origin);
                await p.Until(() => !view.IsPlayingPlaceAnimation, 10f, "設置アニメ完了");
                await p.ExitToGameScreen();
                placed = p.GetBlock(origin) != null && !view.IsPlayingPlaceAnimation;
            }
        }
        sampling.Dispose();
        sampling = null;
        var realSeconds = Time.realtimeSinceStartupAsDouble - started;
        dts.Sort();
        var seconds = dts.Sum();
        var avg = dts.Count / seconds;
        var p50 = dts[(dts.Count - 1) / 2];
        var p95 = dts[Mathf.CeilToInt(dts.Count * 0.95f) - 1];
        var gears = UnityEngine.Object.FindObjectsByType<GearStateChangeProcessor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        var metrics = $"[PERF] avgFps={avg:F3} p50Ms={p50 * 1000:F3} p95Ms={p95 * 1000:F3} p50Fps={1f / p50:F3} p95Fps={1f / p95:F3} frames={dts.Count} dtSeconds={seconds:F3} realSeconds={realSeconds:F3} realFps={dts.Count / realSeconds:F3} screen={Screen.width}x{Screen.height} targetFps={Application.targetFrameRate} vSync={QualitySettings.vSyncCount} captureDelta={Time.captureDeltaTime} blocks={blockCount} gears={gears} terrains={Terrain.activeTerrains.Length} record=true distance={distance:F3} placed={placed} focusedFrames={focusedFrames}/{dts.Count} focused={Application.isFocused}";
        Debug.Log(metrics);
        p.Note(metrics);
        p.Assert(distance > 30f, $"歩行距離30m超: {distance:F2}m");
        p.Assert(focusedFrames == dts.Count, $"全計測フレーム前面: {focusedFrames}/{dts.Count}");
        p.Assert(placed, "60秒内に新規ブロックの設置アニメ完了");
        p.Assert(avg >= 20f, $"平均20fps以上: {avg:F3}");
        await p.Screenshot("02-after-measurement");
    }
    finally
    {
        sampling?.Dispose();
        SemanticInput.KeyUp(Key.W);
        SemanticInput.KeyUp(Key.S);
        // 元のキー有無と保存値を復元する
        // Restore the original key presence and saved value
        GraphicsQualityPreference.SetSettings(savedSettings);
        typeof(GraphicsQualityPreference).GetField("<RecoveredInvalidPreference>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, savedRecovered);
        if (hadPreference) PlayerPrefs.SetString("GraphicsQuality", savedPreference);
        else PlayerPrefs.DeleteKey("GraphicsQuality");
        PlayerPrefs.Save();
        p.Note($"GraphicsQuality復元: existed={hadPreference}, value={savedPreference}");
    }
});
