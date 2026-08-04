// シナリオ: ADR-0007の露頭生成・照準・va:mining往復をライブv8マスタで一括検証する
// Scenario: validates ADR-0007 outcrop creation, aiming, and va:mining round-trip against live v8 master data
using System;
using System.Reflection;
using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Mining;
using Client.Playtest;
using Client.Playtest.Input;
using Core.Master;
using Server.Protocol.PacketResponse;
using UnityEngine;

var stoneVeinGuid = new Guid("735633b7-7aac-4fb8-8b42-022f6bfb9e53");
var expectedMasterVeinCount = 11;
var expectedLayoutOutcropCount = 1772;
var options = new PlaytestRunOptions { Record = true };

return PlaytestRunner.Run("vein-hand-mining-smoke", options, async p =>
{
    // 開幕スキットを本番intentで終了し、ワールド照準を操作可能にする
    // End the opening skit through its production intent so world aiming becomes interactive
    p.Note("開幕スキットを終了し、露頭の起動状態を検証する");
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    var skit = skitStore.GetCurrent();
    if (Array.IndexOf(skit.AllowedIntents, "skip") >= 0)
    {
        var skipResult = skitStore.TrySkip(skit.SessionId, skit.SceneRevision);
        p.Assert(skipResult.Ok, "開幕スキットのSkipインテントが受理された");
        await p.Until(
            () => Array.IndexOf(skitStore.GetCurrent().AllowedIntents, "skip") < 0,
            30f,
            "開幕スキット終了");
    }

    // マスタ11種の実Addressableと、固定レイアウト1772件の生成完了を検証する
    // Verify real Addressables for all 11 masters and completion of all 1,772 fixed-layout instances
    var datastore = UnityEngine.Object.FindFirstObjectByType<OutcropGameObjectDatastore>();
    p.Assert(datastore != null, "OutcropGameObjectDatastoreがMainGameシーンで起動した");
    p.Assert(MasterHolder.MapVeinMaster.All.Count == expectedMasterVeinCount, "ライブv8マスタに鉱脈11種がある");
    foreach (var vein in MasterHolder.MapVeinMaster.All)
    {
        var prefab = AddressableLoader.LoadDefault<GameObject>(vein.OutcropAddressablePath);
        p.Assert(prefab != null, $"露頭Addressable解決: {vein.VeinName}");
    }

    var outcrops = UnityEngine.Object.FindObjectsByType<OutcropGameObject>(
        FindObjectsInactive.Exclude,
        FindObjectsSortMode.None);
    p.Assert(outcrops.Length == expectedLayoutOutcropCount, "固定v8レイアウト1772件の露頭が全て生成された");
    p.Assert(MiningProtocol.ProtocolTag == "va:mining", "手掘りwire tagはva:miningである");

    // 石の斧をホットバー経由で用意し、サーバ権威が読む装備枠へ移して選択する
    // Prepare a stone axe through the hotbar, then move and select it in the server-authoritative equipment slot
    p.Note("石の斧をホットバー1へ入れ、装備枠1で選択する");
    await p.GiveItemToHotbar(0, "石の斧", 1);
    await p.SelectHotbar(0);
    await p.EquipItem("石の斧", 0);

    var stoneOutcrop = datastore.SearchNearestOutcrop(stoneVeinGuid, p.PlayerPosition);
    p.Assert(stoneOutcrop != null, "最寄りの石鉱脈露頭をDatastoreから解決できる");
    var stoneCollider = stoneOutcrop.GetComponentInChildren<Collider>(true);
    p.Assert(stoneCollider != null, "石鉱脈露頭に採掘用Colliderがある");
    p.Assert(stoneCollider.GetComponent<OutcropRayTarget>() != null, "石鉱脈露頭ColliderにOutcropRayTargetがある");

    // 正面と45度方向の双方から実照準し、薄いColliderでもフォーカスを維持できることを確かめる
    // Aim from both front and 45 degrees to prove the thin collider retains focus from practical angles
    await p.Until(
        () => UnityEngine.Object.FindFirstObjectByType<MapObjectMiningController>() != null && Camera.main != null,
        10f,
        "採掘ControllerとMainCameraの起動");
    var controller = UnityEngine.Object.FindFirstObjectByType<MapObjectMiningController>();
    var contextField = typeof(MapObjectMiningController).GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic);
    p.Assert(controller != null, "有効なMapObjectMiningControllerを解決した");
    p.Assert(contextField != null, "採掘Controllerのcontextフィールドを解決した");
    if (contextField == null) throw new InvalidOperationException("MapObjectMiningController._context was not found");
    await p.Until(() => contextField.GetValue(controller) != null, 10f, "採掘Controller contextのDI完了");
    var context = (MapObjectMiningControllerContext)contextField.GetValue(controller);
    var mainCamera = Camera.main;
    var cameraForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
    if (cameraForward.sqrMagnitude < 0.1f) cameraForward = Vector3.forward;

    p.Note("石鉱脈露頭の正面へワープして照準する");
    p.WarpPlayer(stoneOutcrop.transform.position - cameraForward * 0.6f + Vector3.up);
    await p.WaitSeconds(0.5f);
    await p.AimAt(stoneCollider.bounds.center);
    await p.Until(() => ReferenceEquals(context.CurrentFocusTarget, stoneOutcrop), 10f, "正面照準で石露頭をフォーカス");
    await p.Screenshot("01-stone-outcrop-front-focus");

    p.Note("石鉱脈露頭の45度方向へワープして再照準する");
    var angleDirection = Quaternion.Euler(0f, 45f, 0f) * cameraForward;
    p.WarpPlayer(stoneOutcrop.transform.position - angleDirection * 0.6f + Vector3.up);
    await p.WaitSeconds(0.5f);
    await p.AimAt(stoneCollider.bounds.center);
    await p.Until(() => ReferenceEquals(context.CurrentFocusTarget, stoneOutcrop), 10f, "45度照準で石露頭をフォーカス");
    await p.Screenshot("02-stone-outcrop-angle-focus");

    // InputSystemの左クリック保持で採掘FSMを完了させ、サーバ応答後の石増加を待つ
    // Complete the mining FSM with an InputSystem left-button hold and wait for the server-side stone reward
    var stoneBefore = p.CountItem("石");
    p.Note("本番入力の左クリックを保持し、va:miningで石露頭を1回掘る");
    SemanticInput.MouseButtonDown(0);
    await p.WaitSeconds(1.2f);
    SemanticInput.MouseButtonUp(0);
    await p.WaitSeconds(0.5f);
    await p.Until(() => p.CountItem("石") > stoneBefore, 15f, "va:mining応答で石在庫が増加");
    p.Assert(p.CountItem("石") > stoneBefore, "露頭採掘後に石インベントリが増えた");
    await p.Screenshot("03-stone-mined");
    p.Note("ADR-0007 vein手掘りsmoke完了");
});
