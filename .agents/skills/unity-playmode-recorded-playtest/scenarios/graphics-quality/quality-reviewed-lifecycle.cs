using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Client.DebugSystem.Environment;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.GraphicsQuality.Appliers;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// 追加した判定経路を実ワールドの個体で録画する
// Record the revised decision paths on a real world instance
var gameView = Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == "GameView");
gameView.Focus();
await UniTask.DelayFrame(3);
var hadPreference = PlayerPrefs.HasKey("GraphicsQuality");
var savedPreference = PlayerPrefs.GetString("GraphicsQuality");
return PlaytestRunner.Run("quality-reviewed-lifecycle", new PlaytestRunOptions { Record = true }, async p =>
{
    try
    {
        Common.Debug.DebugParameters.SaveInt("DebugEnvironmentTypeKey", 0);
        DebugEnvironmentController.SetEnvironment(DebugEnvironmentType.Debug);
        await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { CreateFlatGround = false, SpawnPosition = new Vector3(659.5f, 96.6f, 606.5f) });
        await p.SkipOpeningSkitIfPlaying();
        GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Medium);
        var origin = new Vector3Int(658, 96, 610);
        p.Assert(p.GetBlock(origin) == null, "蒸気機関の設置先は空");
        p.PlaceBlockDirect("蒸気機関", origin, BlockDirection.North);
        var view = await p.WaitBlockGameObject(origin);
        await p.Until(() => !view.IsPlayingPlaceAnimation, 15f, "実蒸気機関の設置完了");
        var processor = view.GetComponentsInChildren<GearStateChangeProcessor>(true).Single(v => v.RotationInfos.OfType<AnimatorRotationInfo>().Any());
        var rotation = processor.RotationInfos.OfType<AnimatorRotationInfo>().Single();
        var animator = (Animator)typeof(AnimatorRotationInfo).GetField("animator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rotation);
        await p.Until(() => processor.enabled && animator.enabled, 15f, "近距離の回転Animatorが有効");
        p.Assert(animator.runtimeAnimatorController != null, "実Prefabの回転AnimatorControllerを使用");
        await p.Screenshot("01-near-animator");

        // 距離managerによる停止と最新受信状態での再開を観測する
        // Observe manager-driven suspension and resumption with the latest received state
        p.Note("床上の遠距離地点へ移り、実歯車Processorと回転担当Animatorの停止を待つ");
        p.WarpPlayer(new Vector3(659.5f, 96.6f, 526.5f));
        await p.Until(() => !processor.enabled && !animator.enabled, 15f, "遠距離でProcessorとAnimatorが停止");
        var pausedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        await p.WaitSeconds(1f);
        p.Assert(Mathf.Approximately(pausedTime, animator.GetCurrentAnimatorStateInfo(0).normalizedTime), "停止中は実Animatorの進行時刻が不変");
        p.Note($"[GEAR] far enabled={processor.enabled}, animator={animator.enabled}, time={pausedTime}, rpm={processor.DebugCurrentGearState.CurrentRpm}");
        await p.Screenshot("02-far-animator-stopped");
        p.WarpPlayer(new Vector3(659.5f, 96.6f, 606.5f));
        await p.Until(() => processor.enabled && animator.enabled, 15f, "近距離へ戻ると両方再開");
        var rpm60 = (float)typeof(AnimatorRotationInfo).GetField("rpm60Speed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rotation);
        p.Assert(Mathf.Approximately(animator.speed, Mathf.Abs(rpm60 * processor.DebugCurrentGearState.CurrentRpm / 60f)), "実受信中の最新RPMをAnimator速度へ反映");
        p.Note($"[GEAR] near enabled={processor.enabled}, animator={animator.enabled}, speed={animator.speed}, rpm={processor.DebugCurrentGearState.CurrentRpm}");

        // 設定を変えず実破棄だけで保持集合から外れることを確認する
        // Verify actual destruction removes retention without changing settings
        var starts = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IEnumerable<IStartable>>();
        var applier = starts.OfType<EffectsQualityApplier>().Single();
        var switches = (HashSet<EffectsQualitySwitch>)typeof(EffectsQualityApplier).GetField("_switches", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(applier);
        var effects = view.GetComponent<EffectsQualitySwitch>();
        p.Assert(switches.Contains(effects), "実生成個体はEffects保持集合へ登録済み");
        var before = switches.Count;
        p.RemoveBlock(origin);
        await p.Until(() => view == null && !switches.Contains(effects), 15f, "実ブロック破棄だけでEffects保持を解除");
        p.Assert(GraphicsQualityPreference.Current.Equals(GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium)), "破棄の間も中を維持");
        p.Note($"[LIFETIME] before={before}, after={switches.Count}, removed={!switches.Contains(effects)}");
        GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
        await p.WaitSeconds(0.5f);
        GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.High);
        p.Assert(!switches.Contains(effects), "解除後の画質通知でも破棄個体を再保持しない");
        await p.Screenshot("03-after-destruction");
    }
    finally
    {
        // キーの有無と値をそのまま戻す
        // Restore the original key presence and value
        if (hadPreference) PlayerPrefs.SetString("GraphicsQuality", savedPreference);
        else PlayerPrefs.DeleteKey("GraphicsQuality");
        PlayerPrefs.Save();
        p.Note($"GraphicsQuality復元: existed={hadPreference}, value={savedPreference}");
    }
});
