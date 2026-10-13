using System;
using System.Globalization;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.WebUi;
using Core.Master;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.EnergySystem;
using Game.Fluid;
using Game.Gear.Common;
using MessagePack;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("rotary-generator-ui", options, async p =>
{
    var steamPos = new Vector3Int(0, 32, 0);
    var largeFirst = new Vector3Int(3, 32, 6);
    var small = new Vector3Int(3, 33, 9);
    var largeSecond = new Vector3Int(4, 32, 8);
    var generatorPos = new Vector3Int(2, 32, 11);
    var minerPos = new Vector3Int(8, 32, 11);

    // 平地と実プレイ視点を作り、開幕の入力遮断を解除する
    // Create flat ground and the gameplay view, then clear the opening input gate
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    if (p.CurrentUiState == UIStateEnum.GameScreen) p.Note("開幕スキットは再生されず、既にゲーム画面にいる");
    else await p.SkipOpeningSkit();
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "録画の表示言語を日本語に設定");
    InputSystem.AddDevice<Keyboard>();
    InputSystem.AddDevice<Mouse>();
    p.Note("専用仮想キーボードとマウスで実プレイ入力を送る");
    p.WarpPlayer(new Vector3(7f, 34f, 10f));
    await p.WaitSeconds(0.5f);

    p.Note("駆動源なしの回転発電機を F キーで開き、停止表示を確認する");
    var generatorBlock = p.PlaceBlockDirect("回転発電機", generatorPos, BlockDirection.North);
    var generatorObject = await p.WaitBlockGameObject(generatorPos);
    p.Assert(generatorBlock != null && p.GetBlock(generatorPos) != null, "発電機がサーバーに設置された");
    await OpenGenerator();
    await p.UntilWebUiElement("gear-to-electric-section", 15f);
    await p.Until(() => Detail().GenerationState == GearToElectricGenerationState.Stopped, 15f, "RPM不足で停止中");
    await VerifyPanel("停止中（RPM不足）", false);
    await p.Screenshot("rotary-generator-stopped");
    await p.ClickWebUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 蒸気機関から二段の20対10歯噛合で40 RPMへ増速する
    // Increase steam-engine speed to 40 RPM through two 20-to-10 tooth meshes
    p.Note("蒸気機関と二段増速歯車を直設置し、歯車網の連結を確認する");
    p.PlaceBlockDirect("蒸気機関", steamPos, BlockDirection.North);
    await p.WaitSeconds(0.5f);
    p.PlaceBlockDirect("大きな鉄の歯車", largeFirst, BlockDirection.East);
    await p.WaitSeconds(0.5f);
    p.PlaceBlockDirect("鉄の歯車", small, BlockDirection.East);
    await p.WaitSeconds(0.5f);
    p.PlaceBlockDirect("大きな鉄の歯車", largeSecond, BlockDirection.East);
    await p.WaitSeconds(0.5f);
    foreach (var position in new[] { steamPos, largeFirst, small, largeSecond })
    {
        p.Assert(p.GetBlock(position) != null, $"駆動系ブロック設置 {position}");
        await p.WaitBlockGameObject(position);
    }

    var gears = p.ServerService<GearNetworkDatastore>();
    await p.Until(() => gears.TryGetGearNetwork(generatorBlock.BlockInstanceId, out _), 15f, "発電機の歯車網確定");
    p.Assert(gears.TryGetGearNetwork(p.GetBlock(steamPos).BlockInstanceId, out var steamNetwork) &&
             gears.TryGetGearNetwork(generatorBlock.BlockInstanceId, out var generatorNetwork) &&
             steamNetwork.NetworkId.Equals(generatorNetwork.NetworkId), "蒸気機関から発電機まで同一歯車網");

    // 継続補給は準備だけに使い、観測終了時に購読を解除する
    // Use continuous refill only for setup, disposing its subscription after observation
    var fluid = p.GetBlock(steamPos).GetComponent<FuelGearGeneratorFluidComponent>();
    var steamId = MasterHolder.FluidMaster.GetFluidId(Guid.Parse("e2a5d2e5-3fa5-47e2-9fc0-68c8552523f8"));
    using var steamSupply = GameUpdater.UpdateObservable.Subscribe(_ => fluid.AddLiquid(new FluidStack(1d, steamId), default));
    await p.Until(() => Detail().CurrentRpm >= 40f && Detail().GenerationState == GearToElectricGenerationState.Idle, 90f, "40 RPM以上で需要なし待機");
    p.Assert(!fluid.IsPipeDisconnected, "蒸気補給が継続している");

    // 実際のホットバーと電線ツールのクリック操作で需要を接続する
    // Connect demand with the real hotbar and wire-tool click path
    p.Note("電気採掘機を設置し、電線ツールで発電機へ結線する");
    var minerBlock = p.PlaceBlockDirect("電気採掘機", minerPos, BlockDirection.North);
    var minerObject = await p.WaitBlockGameObject(minerPos);
    p.Assert(minerBlock != null, "電気採掘機がサーバーに設置された");
    var generatorWire = generatorBlock.GetComponent<IElectricWireConnector>();
    var minerWire = minerBlock.GetComponent<IElectricWireConnector>();
    p.Assert(!generatorWire.ContainsWireConnection(minerBlock.BlockInstanceId), "結線前は電線がない");
    p.Hotbar.UnlockConnectTool("電線");
    await p.GiveItem("銅のワイヤー", 64);
    await p.Hotbar.AssignHotbar(0, "電線");
    await p.Hotbar.EnterBuildMode(0);
    await AimAtClickCollider(generatorObject);
    await p.ClickPlace();
    await AimAtClickCollider(minerObject);
    await p.ClickPlace();
    await p.Until(() => generatorWire.ContainsWireConnection(minerBlock.BlockInstanceId) &&
                        minerWire.ContainsWireConnection(generatorBlock.BlockInstanceId), 15f, "実操作で双方向電線接続");
    await p.Until(() => Detail().GenerationState == GearToElectricGenerationState.Generating &&
                        Detail().GeneratedElectricPower > 0f, 30f, "実需要へ供給して発電中");
    p.Assert(gears.TryGetGearNetwork(generatorBlock.BlockInstanceId, out var activeNetwork) &&
             activeNetwork.TryResolveRotation(generatorBlock.BlockInstanceId, out var rpm, out _) &&
             rpm.AsPrimitive() >= 40f, "発電中の実RPMは最低40以上");
    await p.ExitToGameScreen();
    await OpenGenerator();
    await p.UntilWebUiElement("gear-to-electric-section", 15f);
    await VerifyPanel("発電中", true);
    await p.Screenshot("rotary-generator-generating");

    // 需要を除いた次の電力tickで供給量がゼロへ戻ることを確認する
    // Verify supply returns to zero on an electric tick after demand removal
    p.Note("採掘機を外し、開いたパネルが待機中と供給0へ同期することを確認する");
    p.Assert(p.RemoveBlock(minerPos), "電力需要を撤去した");
    await p.Until(() => Detail().GenerationState == GearToElectricGenerationState.Idle &&
                        Mathf.Approximately(Detail().GeneratedElectricPower, 0f), 30f, "需要消失後のサーバー状態は待機中・供給0");
    await VerifyPanel("待機中（需要なし）", false);
    await p.Screenshot("rotary-generator-idle");

    #region Internal

    GearToElectricGeneratorBlockStateDetail Detail()
    {
        var component = generatorBlock.GetComponent<GearToElectricGeneratorComponent>();
        var value = component.GetBlockStateDetails().First(d => d.Key == GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey);
        return MessagePackSerializer.Deserialize<GearToElectricGeneratorBlockStateDetail>(value.Value);
    }

    async UniTask AimAtClickCollider(BlockGameObject blockObject)
    {
        var collider = blockObject.GetComponentsInChildren<Collider>(true).First(c => c.name == "ClickCollider");
        var aimPoint = new Vector3(collider.bounds.center.x, collider.bounds.min.y + 1.1f, collider.bounds.center.z);
        await p.AimAt(aimPoint);
    }

    async UniTask OpenGenerator()
    {
        // 通常画面ではAltでカーソル照準を解放し、Fの実操作を通す
        // Free cursor aiming with Alt in gameplay, then use the real F interaction
        SemanticInput.KeyDown(Key.LeftAlt);
        await UniTask.DelayFrame(3);
        await AimAtClickCollider(generatorObject);
        await p.PressInteract();
        SemanticInput.KeyUp(Key.LeftAlt);
        await UniTask.DelayFrame(3);
    }

    async UniTask VerifyPanel(string expectedState, bool expectPositivePower)
    {
        var testIds = new[] { "gear-to-electric-state", "gear-to-electric-supplied-power", "gear-torque", "gear-rpm", "electric-network-section", "gear-network-section" };
        var rows = new Client.Playtest.WebUi.DomQueryResult[testIds.Length];
        for (var attempt = 0; attempt < 30; attempt++)
        {
            for (var index = 0; index < testIds.Length; index++) rows[index] = await PlaytestDomQuery.Query(testIds[index], 1f);
            if (rows.All(row => row.Found) && rows[0].Text == expectedState &&
                rows[1].Text.Contains("最大 100") &&
                (!expectPositivePower || !rows[1].Text.Contains("供給電力 0 "))) break;
            await UniTask.Yield();
        }
        var detail = Detail();
        var current = detail.GeneratedElectricPower.ToString("F0", CultureInfo.InvariantCulture);
        p.Note($"server={detail.GenerationState}, RPM={detail.CurrentRpm:F1}, supplied={detail.GeneratedElectricPower:F1}; DOM={rows[0].Text} / {rows[1].Text}");
        p.Assert(rows.All(row => row.Found), "状態・供給電力・歯車・両ネットワークのDOM行がある");
        p.Assert(rows[0].Text == expectedState, $"DOM状態文字列: {expectedState}");
        p.Assert(detail.GenerationState.ToString() == (expectedState == "発電中" ? "Generating" : expectedState == "待機中（需要なし）" ? "Idle" : "Stopped"), "DOM状態がサーバーenumと一致");
        p.Assert(rows[1].Text == $"供給電力 {current} / 最大 100", "DOM供給量がサーバー値と一致");
        p.Assert(expectPositivePower ? detail.GeneratedElectricPower > 0f : Mathf.Approximately(detail.GeneratedElectricPower, 0f), "サーバー供給量");
        p.Assert(rows[0].Y < rows[1].Y && rows[1].Y < rows[2].Y && rows[2].Y < rows[3].Y &&
                 rows[3].Y < rows[4].Y && rows[4].Y < rows[5].Y, "状態→供給→歯車→電力網→歯車網の表示順");
    }

    #endregion
});
