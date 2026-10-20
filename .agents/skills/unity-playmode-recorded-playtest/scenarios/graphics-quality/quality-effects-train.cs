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
using Game.Block.Interface.Extension;
using Core.Master;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.GraphicsQuality.Appliers;
using Client.Game.InGame.Train.View.Object.Core;
using Game.Block.Blocks.TrainRail;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Client.Game.InGame.Context;
using Game.UnlockState;
using MessagePack;
using UnityEngine.VFX;

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
return PlaytestRunner.Run("quality-effects-train", new PlaytestRunOptions { Record = true }, async p =>
{
    GameObject synthetic = null;
    try
    {
        Common.Debug.DebugParameters.SaveInt("DebugEnvironmentTypeKey", 0);
        DebugEnvironmentController.SetEnvironment(DebugEnvironmentType.Debug);
        await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { CreateFlatGround = false, SpawnPosition = new Vector3(659.5f, 96.6f, 626.5f) });
        await p.SkipOpeningSkitIfPlaying();
        GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Medium);
        SetEffects(false);
        await p.WaitSeconds(0.5f);
        p.Assert(UnityEngine.Object.FindObjectsByType<VisualEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(v => !v.enabled), "既存VFXは全てoff");

        // 実ブロックの設置アニメ後と対象部品数を観測する
        // Observe real components after the block placement animation
        var origin = new Vector3Int(658, 96, 610);
        var engine = p.PlaceBlockDirect("蒸気機関", origin, BlockDirection.North);
        var view = await p.WaitBlockGameObject(origin);
        await p.Until(() => !view.IsPlayingPlaceAnimation, 15f, "Effects off中の新設置アニメ完了");
        var vfx = view.GetComponentsInChildren<VisualEffect>(true);
        p.Assert(view.GetComponent<EffectsQualitySwitch>() != null && vfx.Length > 0 && vfx.All(v => !v.enabled), $"新設置VFX停止: count={vfx.Length}");
        var realParticles = UnityEngine.Object.FindObjectsByType<EffectsQualitySwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None).Sum(v => v.GetComponentsInChildren<ParticleSystem>(true).Length);
        var realLights = UnityEngine.Object.FindObjectsByType<EffectsQualitySwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None).Sum(v => v.GetComponentsInChildren<Light>(true).Length);
        p.Note($"実ブロック/既存列車 particle={realParticles}, light={realLights}");
        p.Assert(view.GetComponentsInChildren<ParticleSystem>(true).All(v => !v.isPlaying), "新設置の実particleは計算停止（0件は件数記録参照）");
        await p.Screenshot("01-new-engine-effects-off");

        // 列車はレールを準備し本番プロトコルで新規生成する
        // Prepare rails and create a new train through the production protocol
        var before = UnityEngine.Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).Select(v => v.TrainCarInstanceId).ToHashSet();
        var a = new Vector3Int(659, 96, 632);
        var b = new Vector3Int(659, 96, 664);
        PlacePier(a);
        await p.WaitSeconds(0.5f);
        PlacePier(b);
        await p.WaitBlockGameObject(a);
        await p.WaitBlockGameObject(b);
        var railA = p.GetBlock(a).GetComponent<RailComponent>();
        var railB = p.GetBlock(b).GetComponent<RailComponent>();
        railA.FrontNode.ConnectNode(railB.FrontNode);
        railB.BackNode.ConnectNode(railA.BackNode);
        var master = MasterHolder.TrainUnitMaster.Train.TrainCars.First(v => v.TractionForce > 0);
        p.ServerService<IGameUnlockStateDataController>().UnlockTrainCar(master.TrainCarGuid);
        foreach (var item in master.RequiredItems) p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(item.ItemGuid).Name, item.Count);
        var position = new RailPosition(new List<IRailNode> { railA.BackNode, railB.BackNode }, TrainLengthConverter.ToRailUnits(master.Length), 0);
        var response = await ClientContext.VanillaApi.Response.Train.PlaceTrainOnRail(position, master.TrainCarGuid, CancellationToken.None);
        p.Assert(response != null && response.Success, "機関車の本番設置プロトコル成功");
        TrainCarEntityObject Spawned() => UnityEngine.Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault(v => !before.Contains(v.TrainCarInstanceId));
        await p.Until(() => Spawned() != null, 20f, "新規機関車のクライアント生成");
        var train = Spawned();
        var trainVfx = train.GetComponentsInChildren<VisualEffect>(true);
        p.Assert(train.GetComponent<EffectsQualitySwitch>() != null && trainVfx.Length > 0 && trainVfx.All(v => !v.enabled), $"新規列車へSwitch付与とVFX停止: {trainVfx.Length}");
        SetEffects(true);
        await p.WaitSeconds(0.5f);
        p.Assert(trainVfx.All(v => v.enabled), "新規列車のVFX再開");
        await p.Screenshot("02-new-locomotive");

        // 合成particleは実prefabと区別して再有効化と手動Playを観測する
        // Distinguish synthetic particles from real prefabs and observe reactivation and manual Play
        synthetic = new GameObject("Task7SyntheticParticleRoot");
        var child = new GameObject("SyntheticPlayOnAwake");
        child.transform.SetParent(synthetic.transform);
        var particle = child.AddComponent<ParticleSystem>();
        var main = particle.main;
        main.playOnAwake = true;
        child.SetActive(false);
        var quality = synthetic.AddComponent<EffectsQualitySwitch>();
        quality.Initialize(false);
        child.SetActive(true);
        await p.WaitSeconds(0.5f);
        quality.SetEffectsEnabled(false);
        p.Assert(!particle.isPlaying, "合成playOnAwake再有効化後のoff再適用で停止");
        particle.Play(false);
        await p.WaitSeconds(0.5f);
        quality.SetEffectsEnabled(false);
        p.Assert(!particle.isPlaying, "合成手動Play後のoff再適用で停止");
    }
    finally
    {
        if (synthetic != null) UnityEngine.Object.Destroy(synthetic);
        if (hadPreference) PlayerPrefs.SetString("GraphicsQuality", savedPreference);
        else PlayerPrefs.DeleteKey("GraphicsQuality");
        PlayerPrefs.Save();
    }

    #region Internal
    void SetEffects(bool enabled)
    {
        var s = GraphicsQualityPreference.Current;
        GraphicsQualityPreference.SetSettings(new GraphicsQualitySettings(s.DrawDistance, s.ShadowsEnabled, s.RenderScale, s.SsaoEnabled, s.PostProcessingEnabled, enabled, s.MapObjectsVisible, s.TerrainDetail, s.OceanVisible));
    }
    void PlacePier(Vector3Int position)
    {
        var id = Client.Playtest.Operations.PlaytestBlockOps.ResolveBlockId("レール橋脚");
        var detail = new RailBridgePierComponentStateDetail(RailComponent.ToVector3(BlockDirection.North));
        var parameters = new[] { new BlockCreateParam(RailBridgePierComponentStateDetail.StateDetailKey, MessagePackSerializer.Serialize(detail)) };
        p.Assert(ServerContext.WorldBlockDatastore.TryAddBlock(id, position, BlockDirection.North, parameters, out _), $"橋脚設置 {position}");
    }
    #endregion
});
