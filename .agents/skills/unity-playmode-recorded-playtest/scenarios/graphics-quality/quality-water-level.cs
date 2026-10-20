using System.Linq;
using Client.DebugSystem.Environment;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Fluid;
using Core.Master;

// 録画前に入力とGameViewを実確認する
// Probe input and Game View before starting the recording
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
return PlaytestRunner.Run("quality-water-level", new PlaytestRunOptions { Record = true }, async p =>
{
    try
    {
        // 原本の地形を変えず、孤立パイプへ本物の流体状態を送る
        // Send real fluid state to an isolated pipe without altering the source terrain
        Common.Debug.DebugParameters.SaveInt("DebugEnvironmentTypeKey", 0);
        DebugEnvironmentController.SetEnvironment(DebugEnvironmentType.Debug);
        var normalRoot = UnityEngine.Object.FindFirstObjectByType<DebugEnvironmentObjectRoot>(FindObjectsInactive.Include);
        p.Assert(normalRoot.gameObject.activeInHierarchy, "通常再生設定Debugの環境ルートが有効");
        p.Note($"[ENV] Debug={normalRoot.gameObject.activeInHierarchy}, terrain={Terrain.activeTerrains.Length}, environment={Common.Debug.DebugParameters.GetValueOrDefaultInt("DebugEnvironmentTypeKey", -1)}");
        await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { CreateFlatGround = false, SpawnPosition = new Vector3(657.5f, 96.6f, 627.5f) });
        await p.SkipOpeningSkitIfPlaying();
        GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Medium);
        var origin = new Vector3Int(659, 96, 630);
        p.Assert(p.GetBlock(origin) == null, "孤立パイプのセルは空");
        var block = p.PlaceBlockDirect("鉄のパイプ", origin, BlockDirection.East);
        var view = await p.WaitBlockGameObject(origin);
        await p.Until(() => !view.IsPlayingPlaceAnimation, 15f, "パイプ設置アニメ完了");
        var processor = view.GetComponentInChildren<FluidPipeStateChangeProcessor>();
        var renderer = (MeshRenderer)typeof(FluidPipeStateChangeProcessor).GetField("meshRenderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(processor);
        p.Assert(typeof(FluidPipeStateChangeProcessor).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) == null, "水位ProcessorにUpdate無し");
        var pipe = block.GetComponent<FluidPipeComponent>();
        var water = MasterHolder.FluidMaster.GetFluidId(new System.Guid("9eae6979-d56a-4991-9107-b8161acec430"));
        var total = 0f;
        foreach (var added in new[] { 100f, 150f, 200f })
        {
            p.Note($"サーバーへ水を{added}投入し状態受信から水位を表示する");
            pipe.Node.AddExternal(new FluidStack(added, water));
            total += added;
            var expected = total / 500f;
            await p.Until(() => processor.DebugCurrentFluidPipeState != null && Mathf.Abs(renderer.sharedMaterial.GetFloat("_WaterLevel") - expected) < 0.02f, 15f, $"水位受信 {expected:F1}");
            var actual = renderer.sharedMaterial.GetFloat("_WaterLevel");
            p.Assert(Mathf.Abs(actual - expected) < 0.02f, $"水位実測 {actual:F3}, received={processor.DebugCurrentFluidPipeState.Amount}/{processor.DebugCurrentFluidPipeState.Capacity}");
            await p.Screenshot($"water-{total:F0}");
            await p.WaitSeconds(0.5f);
        }
    }
    finally
    {
        // キー有無も含めて永続設定を戻す
        // Restore persistent settings including key presence
        if (hadPreference) PlayerPrefs.SetString("GraphicsQuality", savedPreference);
        else PlayerPrefs.DeleteKey("GraphicsQuality");
        PlayerPrefs.Save();
    }
});
