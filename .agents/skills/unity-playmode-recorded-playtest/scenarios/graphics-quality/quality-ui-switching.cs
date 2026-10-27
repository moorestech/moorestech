using System.Linq;
using Client.DebugSystem.Environment;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.FluidNetwork.View;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Fluid;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// 録画前に入力とGameViewを実確認する
// Probe input and Game View before starting the recording
var gameView = Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == "GameView");
var viewFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var dock = typeof(UnityEditor.EditorWindow).GetField("m_Parent", viewFlags).GetValue(gameView);
var tabs = (System.Collections.IList)dock.GetType().GetField("m_Panes", viewFlags).GetValue(dock);
dock.GetType().GetProperty("selected", viewFlags).SetValue(dock, tabs.IndexOf(gameView));
gameView.Focus();
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
return PlaytestRunner.Run("quality-ui-switching", new PlaytestRunOptions { Record = true }, async p =>
{
    try
    {
        // 固定worldのOtherを通常再生のDebug環境へ揃える
        // Match the normal Debug environment instead of fixed-world Other
        Common.Debug.DebugParameters.SaveInt("DebugEnvironmentTypeKey", 0);
        DebugEnvironmentController.SetEnvironment(DebugEnvironmentType.Debug);
        await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { CreateFlatGround = false, SpawnPosition = new Vector3(657.5f, 96.6f, 627.5f) });
        await p.SkipOpeningSkitIfPlaying();
        GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Medium);
        var normalRoot = UnityEngine.Object.FindFirstObjectByType<DebugEnvironmentObjectRoot>(FindObjectsInactive.Include);
        p.Assert(normalRoot.gameObject.activeInHierarchy, "通常再生設定Debugの環境ルートが有効");
        p.Note($"[ENV] Debug={normalRoot.gameObject.activeInHierarchy}, terrain={Terrain.activeTerrains.Length}, environment={Common.Debug.DebugParameters.GetValueOrDefaultInt("DebugEnvironmentTypeKey", -1)}");

        // 切替前の同じパイプを以後の検証まで保持する
        // Keep this pipe alive across all quality changes and water checks
        // 水はこのパイプへ直接入れず、持ち越し(未構築)で満たした隣のパイプを足して網全体の水位で釣り合わせる。網に入ったパイプへの直接投入はクライアント側のコピーと食い違う
        // Water is never pushed into this pipe directly; prefilled neighbor pipes (filled while unbuilt) are added and the network settles to one fill level. Direct injection into a built-in pipe would diverge from the client-side copy
        var origin = new Vector3Int(659, 96, 630);
        var neighborPositions = new[] { new Vector3Int(660, 96, 630), new Vector3Int(661, 96, 630), new Vector3Int(662, 96, 630) };
        p.Assert(p.GetBlock(origin) == null && neighborPositions.All(c => p.GetBlock(c) == null), "孤立パイプと隣のセルは空");
        var block = p.PlaceBlockDirect("鉄のパイプ", origin, BlockDirection.East);
        var view = await p.WaitBlockGameObject(origin);
        await p.Until(() => !view.IsPlayingPlaceAnimation, 15f, "パイプ設置アニメ完了");
        var waterLevelView = view.GetComponentInChildren<FluidPipeWaterLevelView>();
        var renderer = (MeshRenderer)typeof(FluidPipeWaterLevelView).GetField("meshRenderer", viewFlags).GetValue(waterLevelView);
        p.Assert(typeof(FluidPipeWaterLevelView).GetMethod("Update", viewFlags) == null, "水位ViewにUpdate無し");
        var pipe = block.GetComponent<FluidPipeComponent>();
        var networkWater = 0.0;
        var networkPipes = 1;
        p.Note($"[PIPE] before UI: block={block.BlockInstanceId}, view={view.GetInstanceID()}, waterLevelView={waterLevelView.GetInstanceID()}, renderer={renderer.GetInstanceID()}, origin={origin}");
        await AddWater(neighborPositions[0], 200.0);
        await p.Screenshot("01-medium-game");
        p.Note("Escapeからトップ4ボタンを確認して画質設定を開く");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 15f);
        foreach (var id in new[] { "pause-menu-save", "pause-menu-save-and-quit", "pause-menu-open-settings", "pause-menu-open-bug-report" })
        {
            await p.UntilUiElement(id, 15f);
            var rect = await PlaytestUiElementOps.FindUi(id);
            var screenRect = rect.ScreenRect;
            p.Assert(rect.Visible && screenRect.xMin >= 0 && screenRect.yMin >= 0 && screenRect.xMax <= Screen.width && screenRect.yMax <= Screen.height, $"トップ見切れなし {id}: {screenRect}");
        }
        await p.Screenshot("02-pause-top");
        await p.ClickUi("pause-menu-open-settings");
        await Choose("graphics-preset-low");
        AssertPreset(GraphicsQualityPreset.Low, 300f, false, 0.5f);
        await CloseSettings();
        await p.Screenshot("03-low-game");

        // 高へ戻してから影だけを変えカスタム表示を確認する
        // Restore High, then change shadows alone and verify Custom
        await OpenSettings();
        await Choose("graphics-preset-high");
        AssertPreset(GraphicsQualityPreset.High, 5000f, true, 1f);
        await CloseSettings();
        await p.Screenshot("04-high-game");
        await OpenSettings();
        await Choose("graphics-shadows-off");
        await p.UntilUiElement("graphics-preset-custom", 15f);
        p.Assert(!GraphicsQualityPresets.TryMatch(GraphicsQualityPreference.Current, out _), "影だけオフでカスタム");
        await p.Screenshot("05-custom-settings");
        await CloseSettings();
        await p.Screenshot("06-custom-game");

        // 中へ戻さず、再設置せず、同じ受信先の水位を変える
        // Change water on the same receiver without resetting quality or replacing it
        p.Assert(object.ReferenceEquals(p.GetBlock(origin), block) && object.ReferenceEquals(await p.WaitBlockGameObject(origin), view), "UI切替前後で同一サーバーブロックとView");
        p.Assert(view.GetComponentInChildren<FluidPipeWaterLevelView>() == waterLevelView && (MeshRenderer)typeof(FluidPipeWaterLevelView).GetField("meshRenderer", viewFlags).GetValue(waterLevelView) == renderer, "UI切替前後で同一水位ViewとRenderer");
        p.Note($"[PIPE] after UI: block={block.BlockInstanceId}, view={view.GetInstanceID()}, waterLevelView={waterLevelView.GetInstanceID()}, renderer={renderer.GetInstanceID()}");
        await AddWater(neighborPositions[1], 450.0);
        await AddWater(neighborPositions[2], 500.0);
        p.Assert(!GraphicsQualityPresets.TryMatch(GraphicsQualityPreference.Current, out _) && !Camera.main.GetUniversalAdditionalCameraData().renderShadows, "水位確認完了まで影オフのカスタムを維持");
    }
    finally
    {
        // 値だけでなく未保存状態も必ず戻す
        // Always restore both the value and the absence of a saved key
        GraphicsQualityPreference.SetSettings(savedSettings);
        typeof(GraphicsQualityPreference).GetField("<RecoveredInvalidPreference>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, savedRecovered);
        if (hadPreference) PlayerPrefs.SetString("GraphicsQuality", savedPreference);
        else PlayerPrefs.DeleteKey("GraphicsQuality");
        PlayerPrefs.Save();
        p.Note($"GraphicsQuality復元: existed={hadPreference}, value={savedPreference}");
    }

    #region Internal
    async UniTask AddWater(Vector3Int neighborPosition, double added)
    {
        // 水を持ち越しで入れた隣のパイプを足す。網は充填率(水位)で釣り合うので、同じパイプの水位は 網の総量 / 網の総容量 に向かう。サーバーの量と材質の両方がそこへ届いてから撮影する
        // Add a neighbor pipe prefilled through its holdover; the network balances by fill rate, so this pipe's level approaches total water / total capacity. Capture only after both the server amount and the material reach it
        var water = MasterHolder.FluidMaster.GetFluidId(new System.Guid("9eae6979-d56a-4991-9107-b8161acec430"));
        var neighbor = p.PlaceBlockDirect("鉄のパイプ", neighborPosition, BlockDirection.East).GetComponent<FluidPipeComponent>();
        var remainder = neighbor.AddLiquid(new FluidStack(added, water), default);
        p.Assert(neighbor.NodeIndex < 0 && remainder.Amount == 0, $"未構築の隣パイプへ持ち越しで水{added}を注入");
        networkWater += added;
        networkPipes++;
        var expected = (float)(networkWater / (pipe.Capacity * networkPipes));
        p.Note($"隣のパイプ{neighborPosition}に水{added}を足す: 網 {networkWater}/{pipe.Capacity * networkPipes}, 期待水位 {expected:F3}");
        await p.Until(() => Mathf.Abs((float)(pipe.Amount / pipe.Capacity) - expected) < 0.01f && Mathf.Abs(renderer.sharedMaterial.GetFloat("_WaterLevel") - expected) < 0.02f, 15f, $"水位到達 {expected:F3}");
        p.Assert(Mathf.Abs(waterLevelView.DebugWaterLevel - expected) < 0.02f, $"水位View {waterLevelView.DebugWaterLevel:F3}, server={pipe.Amount:F1}/{pipe.Capacity}");
        p.Assert(Mathf.Abs(renderer.sharedMaterial.GetFloat("_WaterLevel") - expected) < 0.02f, $"水位実測 {renderer.sharedMaterial.GetFloat("_WaterLevel"):F3}, server={pipe.Amount:F1}/{pipe.Capacity}");
        p.Assert(renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.isVisible, "同じパイプの水位Rendererが可視");
        await p.Screenshot($"water-{expected * 100:F0}pct");
        await p.WaitSeconds(0.5f);
    }
    async UniTask Choose(string id)
    {
        await PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable(id, "pause-menu-body", 15f);
        await p.ClickUi(id);
    }
    async UniTask OpenSettings()
    {
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 15f);
        await p.ClickUi("pause-menu-open-settings");
    }
    async UniTask CloseSettings()
    {
        await p.PressKey(Key.Escape);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 15f);
    }
    void AssertPreset(GraphicsQualityPreset expected, float distance, bool shadows, float scale)
    {
        p.Assert(GraphicsQualityPresets.TryMatch(GraphicsQualityPreference.Current, out var actual) && actual == expected, $"プリセット {expected}");
        p.Assert(Mathf.Approximately(Camera.main.farClipPlane, distance), $"farClip={Camera.main.farClipPlane}");
        p.Assert(Camera.main.GetUniversalAdditionalCameraData().renderShadows == shadows, $"shadows={shadows}");
        p.Assert(Mathf.Approximately(((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale, scale), $"renderScale={scale}");
    }
    #endregion
});
