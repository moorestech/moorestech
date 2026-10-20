using System.Linq;
using Client.DebugSystem.Environment;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Fluid;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Client.Playtest.WebUi;
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
        var origin = new Vector3Int(659, 96, 630);
        p.Assert(p.GetBlock(origin) == null, "孤立パイプのセルは空");
        var block = p.PlaceBlockDirect("鉄のパイプ", origin, BlockDirection.East);
        var view = await p.WaitBlockGameObject(origin);
        await p.Until(() => !view.IsPlayingPlaceAnimation, 15f, "パイプ設置アニメ完了");
        var processor = view.GetComponentInChildren<FluidPipeStateChangeProcessor>();
        var renderer = (MeshRenderer)typeof(FluidPipeStateChangeProcessor).GetField("meshRenderer", viewFlags).GetValue(processor);
        p.Assert(typeof(FluidPipeStateChangeProcessor).GetMethod("Update", viewFlags) == null, "水位ProcessorにUpdate無し");
        var pipe = block.GetComponent<FluidPipeComponent>();
        p.Note($"[PIPE] before UI: block={block.BlockInstanceId}, view={view.GetInstanceID()}, processor={processor.GetInstanceID()}, renderer={renderer.GetInstanceID()}, origin={origin}");
        await AddWater(pipe, processor, renderer, 100f, 100f);
        await p.Screenshot("01-medium-game");
        p.Note("Escapeからトップ4ボタンを確認して画質設定を開く");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 15f);
        foreach (var id in new[] { "pause-menu-save", "pause-menu-save-and-quit", "pause-menu-open-settings", "pause-menu-open-bug-report" })
        {
            await p.UntilWebUiElement(id, 15f);
            var rect = await PlaytestDomQuery.Query(id, 5f);
            p.Assert(rect.Visible && rect.X >= 0 && rect.Y >= 0 && rect.X + rect.Width <= Screen.width && rect.Y + rect.Height <= Screen.height, $"トップ見切れなし {id}: {rect.X},{rect.Y},{rect.Width},{rect.Height}");
        }
        await p.Screenshot("02-pause-top");
        await p.ClickWebUi("pause-menu-open-settings");
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
        await p.UntilWebUiElement("graphics-preset-custom", 15f);
        p.Assert(!GraphicsQualityPresets.TryMatch(GraphicsQualityPreference.Current, out _), "影だけオフでカスタム");
        await p.Screenshot("05-custom-settings");
        await CloseSettings();
        await p.Screenshot("06-custom-game");

        // 中へ戻さず、再設置せず、同じ受信先の水位を変える
        // Change water on the same receiver without resetting quality or replacing it
        p.Assert(object.ReferenceEquals(p.GetBlock(origin), block) && object.ReferenceEquals(await p.WaitBlockGameObject(origin), view), "UI切替前後で同一サーバーブロックとView");
        p.Assert(view.GetComponentInChildren<FluidPipeStateChangeProcessor>() == processor && (MeshRenderer)typeof(FluidPipeStateChangeProcessor).GetField("meshRenderer", viewFlags).GetValue(processor) == renderer, "UI切替前後で同一ProcessorとRenderer");
        p.Note($"[PIPE] after UI: block={block.BlockInstanceId}, view={view.GetInstanceID()}, processor={processor.GetInstanceID()}, renderer={renderer.GetInstanceID()}");
        await AddWater(pipe, processor, renderer, 150f, 250f);
        await AddWater(pipe, processor, renderer, 200f, 450f);
        p.Assert(!GraphicsQualityPresets.TryMatch(GraphicsQualityPreference.Current, out _) && !Camera.main.GetUniversalAdditionalCameraData().renderShadows, "水位確認完了まで影オフのカスタムを維持");
    }
    finally
    {
        // 値だけでなく未保存状態も必ず戻す
        // Always restore both the value and the absence of a saved key
        if (hadPreference) PlayerPrefs.SetString("GraphicsQuality", savedPreference);
        else PlayerPrefs.DeleteKey("GraphicsQuality");
        PlayerPrefs.Save();
        p.Note($"GraphicsQuality復元: existed={hadPreference}, value={savedPreference}");
    }

    #region Internal
    async UniTask AddWater(FluidPipeComponent pipe, FluidPipeStateChangeProcessor processor, MeshRenderer renderer, float added, float total)
    {
        // サーバー状態と材質の両方が届いてから撮影する
        // Capture only after both server state and material reflect the new amount
        var water = MasterHolder.FluidMaster.GetFluidId(new System.Guid("9eae6979-d56a-4991-9107-b8161acec430"));
        p.Note($"同じパイプへ水を{added}投入: 合計{total}/500");
        pipe.Node.AddExternal(new FluidStack(added, water));
        var expected = total / 500f;
        await p.Until(() => processor.DebugCurrentFluidPipeState != null && processor.DebugCurrentFluidPipeState.Amount == total && Mathf.Abs(renderer.sharedMaterial.GetFloat("_WaterLevel") - expected) < 0.02f, 15f, $"水位受信 {expected:F1}");
        p.Assert(Mathf.Abs(renderer.sharedMaterial.GetFloat("_WaterLevel") - expected) < 0.02f, $"水位実測 {renderer.sharedMaterial.GetFloat("_WaterLevel"):F3}, received={processor.DebugCurrentFluidPipeState.Amount}/{processor.DebugCurrentFluidPipeState.Capacity}");
        p.Assert(renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.isVisible, "同じパイプの水位Rendererが可視");
        await p.Screenshot($"water-{total:F0}");
        await p.WaitSeconds(0.5f);
    }
    async UniTask Choose(string id)
    {
        await PlaytestWebUiOps.ScrollDownUntilWebUiElementIsClickable(id, "pause-menu-body", 15f);
        await p.ClickWebUi(id);
    }
    async UniTask OpenSettings()
    {
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 15f);
        await p.ClickWebUi("pause-menu-open-settings");
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
