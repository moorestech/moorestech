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
using Game.Block.Interface;

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
var savedSettings = GraphicsQualityPreference.Current;
var savedRecovered = GraphicsQualityPreference.RecoveredInvalidPreference;
return PlaytestRunner.Run("quality-ssao-visual", new PlaytestRunOptions { Record = true }, async p =>
{
    try
    {
        Common.Debug.DebugParameters.SaveInt("DebugEnvironmentTypeKey", 0);
        DebugEnvironmentController.SetEnvironment(DebugEnvironmentType.Debug);
        await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { CreateFlatGround = false, SpawnPosition = new Vector3(658.0f, 96.6f, 626.5f) });
        await p.SkipOpeningSkitIfPlaying();
        GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Medium);
        var origin = new Vector3Int(660, 96, 630);
        p.PlaceBlockDirect("木のチェスト", origin, BlockDirection.North);
        var view = await p.WaitBlockGameObject(origin);
        await p.Until(() => !view.IsPlayingPlaceAnimation, 15f, "比較用チェスト設置完了");
        await p.WaitSeconds(2f);
        p.Note("SSAO以外の条件を保ち、通常カメラの停止状態で画面差を採る");
        var on1 = await p.Screenshot("01-ssao-on");
        await p.WaitSeconds(0.5f);
        var on2 = await p.Screenshot("02-ssao-on-noise");
        var s = GraphicsQualityPreference.Current;
        GraphicsQualityPreference.SetSettings(new GraphicsQualitySettings(s.DrawDistance, s.ShadowsEnabled, s.RenderScale, false, s.PostProcessingEnabled, s.EffectsEnabled, s.MapObjectsVisible, s.TerrainDetail, s.OceanVisible));
        await p.WaitSeconds(0.5f);
        var off = await p.Screenshot("03-ssao-off");

        // オーバーレイ領域を除いた画素差を同条件のノイズと比較する
        // Compare pixel differences outside the overlay against same-setting noise
        var a = Load(on1);
        var b = Load(on2);
        var c = Load(off);
        var noise = Difference(a, b);
        var changed = Difference(b, c);
        p.Note($"SSAO画素差: on/on={noise:F6}, on/off={changed:F6}");
        p.Assert(changed > noise + 0.05f, $"SSAOの画面差が同設定ノイズを超える: {changed:F6}>{noise:F6}");
        UnityEngine.Object.Destroy(a);
        UnityEngine.Object.Destroy(b);
        UnityEngine.Object.Destroy(c);
    }
    finally
    {
        GraphicsQualityPreference.SetSettings(savedSettings);
        typeof(GraphicsQualityPreference).GetField("<RecoveredInvalidPreference>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, savedRecovered);
        if (hadPreference) PlayerPrefs.SetString("GraphicsQuality", savedPreference);
        else PlayerPrefs.DeleteKey("GraphicsQuality");
        PlayerPrefs.Save();
    }

    #region Internal
    Texture2D Load(string path)
    {
        var image = new Texture2D(2, 2);
        if (!ImageConversion.LoadImage(image, System.IO.File.ReadAllBytes(path))) throw new System.InvalidOperationException("PNG decode failed");
        return image;
    }
    double Difference(Texture2D a, Texture2D b)
    {
        var x = a.GetPixels32();
        var y = b.GetPixels32();
        double sum = 0;
        var count = 0;
        for (var row = 120; row < 400; row++)
        for (var column = 700; column < 1200; column++)
        {
            var i = row * a.width + column;
            sum += System.Math.Abs(x[i].r - y[i].r) + System.Math.Abs(x[i].g - y[i].g) + System.Math.Abs(x[i].b - y[i].b);
            count += 3;
        }
        return sum / count;
    }
    #endregion
});
