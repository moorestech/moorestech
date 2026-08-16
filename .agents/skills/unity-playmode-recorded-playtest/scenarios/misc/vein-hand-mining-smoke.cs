// ライブv8採掘smoke
// Live v8 mining smoke
using System;
using System.Reflection;
using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Mining;
using Client.Playtest;
using Client.Playtest.Input;
using Cysharp.Threading.Tasks;
using Core.Master;
using Server.Protocol.PacketResponse;
using UnityEngine;

var stoneVeinGuid = new Guid("735633b7-7aac-4fb8-8b42-022f6bfb9e53");
var expectedMasterVeinCount = 11;
var expectedLayoutOutcropCount = 1775;
var options = new PlaytestRunOptions { Record = true };

return PlaytestRunner.Run("vein-hand-mining-smoke", options, async p =>
{
    // 開幕スキット終了
    // End opening skit
    p.Note("開幕スキット(blocking-skit)の表示を待つ");
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    var skitShown = await PollUntilAsync(async () =>
        (await Client.Playtest.WebUi.PlaytestDomQuery.Query("blocking-skit", 1f)).Found, 30);
    p.Assert(skitShown, "開幕スキット(blocking-skit)がWeb HUDに表示された");

    // 受理されるまでSkipを試み、終了はskip自身では満たせないDOM消失で確かめる
    // Retry Skip until accepted, then confirm the end by the DOM disappearing, which skip itself cannot satisfy
    p.Note("Skipインテントで開幕スキットを飛ばす");
    var skipAccepted = await PollUntil(() =>
    {
        var current = skitStore.GetCurrent();
        return skitStore.TrySkip(current.SessionId, current.SceneRevision).Ok;
    }, 15);
    p.Assert(skipAccepted, "開幕スキットのSkipインテントが受理された");
    var skitGone = await PollUntilAsync(async () =>
        !(await Client.Playtest.WebUi.PlaytestDomQuery.Query("blocking-skit", 1f)).Found, 30);
    p.Assert(skitGone, "開幕スキットが終了した");

    // 11種・1775件を検証
    // Verify 11 kinds and 1,775 instances
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
    p.Assert(outcrops.Length == expectedLayoutOutcropCount, "固定v8レイアウト1775件の露頭が全て生成された");
    p.Assert(MiningProtocol.ProtocolTag == "va:mining", "手掘りwire tagはva:miningである");

    // 石斧を装備枠へ設定
    // Set stone axe to equipment slot
    p.Note("石の斧をホットバー1へ入れ、装備枠1で選択する");
    await p.GiveItemToHotbar(0, "石の斧", 1);
    await p.SelectHotbar(0);
    await p.EquipItem("石の斧", 0);

    var stoneOutcrop = datastore.SearchNearestOutcrop(stoneVeinGuid, p.PlayerPosition);
    p.Assert(stoneOutcrop != null, "最寄りの石鉱脈露頭をDatastoreから解決できる");
    var stoneCollider = stoneOutcrop.GetComponentInChildren<Collider>(true);
    p.Assert(stoneCollider != null, "石鉱脈露頭に採掘用Colliderがある");
    p.Assert(stoneCollider.GetComponent<OutcropRayTarget>() != null, "石鉱脈露頭ColliderにOutcropRayTargetがある");

    // 2方向の照準を検証
    // Verify aiming from two directions
    await p.Until(
        () => UnityEngine.Object.FindFirstObjectByType<MiningController>() != null && Camera.main != null,
        10f,
        "採掘ControllerとMainCameraの起動");
    var controller = UnityEngine.Object.FindFirstObjectByType<MiningController>();
    var contextField = typeof(MiningController).GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic);
    p.Assert(controller != null, "有効なMiningControllerを解決した");
    p.Assert(contextField != null, "採掘Controllerのcontextフィールドを解決した");
    if (contextField == null) throw new InvalidOperationException("MiningController._context was not found");
    await p.Until(() => contextField.GetValue(controller) != null, 10f, "採掘Controller contextのDI完了");
    var context = (MiningControllerContext)contextField.GetValue(controller);
    var mainCamera = Camera.main;
    var cameraForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
    if (cameraForward.sqrMagnitude < 0.1f) cameraForward = Vector3.forward;

    p.Note("石鉱脈露頭の正面1.4m地点へワープして照準する");
    p.WarpPlayer(stoneOutcrop.transform.position - cameraForward * 1.4f + Vector3.up * 0.5f);
    await p.WaitSeconds(0.5f);
    await p.AimAt(stoneCollider.bounds.center);
    await p.Until(() => ReferenceEquals(context.CurrentFocusTarget, stoneOutcrop), 10f, "正面照準で本番focusが石露頭と一致");

    // 本番focus確認後だけ輪郭を表示する
    // Show the evidence outline only after production focus matches
    var outlineObject = new GameObject("PlaytestStoneOutcropEvidenceOutline");
    Material outlineMaterial = null;
    try
    {
        var outline = outlineObject.AddComponent<LineRenderer>();
        outlineMaterial = new Material(Shader.Find("Sprites/Default"));
        outline.material = outlineMaterial;
        outline.startColor = Color.magenta;
        outline.endColor = Color.magenta;
        outline.startWidth = 0.12f;
        outline.endWidth = 0.12f;
        outline.positionCount = 5;
        var bounds = stoneCollider.bounds;
        var outlineY = bounds.max.y + 0.15f;
        outline.SetPositions(new[]
        {
            new Vector3(bounds.min.x, outlineY, bounds.min.z),
            new Vector3(bounds.max.x, outlineY, bounds.min.z),
            new Vector3(bounds.max.x, outlineY, bounds.max.z),
            new Vector3(bounds.min.x, outlineY, bounds.max.z),
            new Vector3(bounds.min.x, outlineY, bounds.min.z),
        });
        p.Note("本番focus一致済みの石露頭に証跡用マゼンタ輪郭を表示する");
        await p.Screenshot("01-stone-outcrop-front-focus");

        p.Note("石鉱脈露頭の45度方向1.4m地点へワープして再照準する");
        var angleDirection = Quaternion.Euler(0f, 45f, 0f) * cameraForward;
        p.WarpPlayer(stoneOutcrop.transform.position - angleDirection * 1.4f + Vector3.up * 0.5f);
        await p.WaitSeconds(0.5f);
        await p.AimAt(stoneCollider.bounds.center);
        await p.Until(() => ReferenceEquals(context.CurrentFocusTarget, stoneOutcrop), 10f, "45度照準で本番focusが石露頭と一致");
        await p.Screenshot("02-stone-outcrop-angle-focus");

        // 採掘後の石増加を待つ
        // Wait for stone increase after mining
        var stoneBefore = p.CountItem("石");
        p.Note("本番入力の左クリックを保持し、va:miningで石露頭を1回掘る");
        SemanticInput.MouseButtonDown(0);
        await p.WaitSeconds(1.2f);
        SemanticInput.MouseButtonUp(0);
        await p.WaitSeconds(0.5f);
        await p.Until(() => stoneBefore < p.CountItem("石"), 15f, "va:mining応答で石在庫が増加");
        p.Assert(stoneBefore < p.CountItem("石"), "露頭採掘後に石インベントリが増えた");
        await p.Screenshot("03-stone-mined");
        p.Note("ADR-0007 vein手掘りsmoke完了");
    }
    finally
    {
        // 失敗時も入力と証跡を確実に解除する
        // Always release input and evidence artifacts on failure
        SemanticInput.MouseButtonUp(0);
        UnityEngine.Object.Destroy(outlineObject);
        UnityEngine.Object.Destroy(outlineMaterial);
    }

    #region Internal

    async UniTask<bool> PollUntil(Func<bool> condition, int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            if (condition()) return true;
            await p.WaitSeconds(1f);
        }
        return condition();
    }

    async UniTask<bool> PollUntilAsync(Func<UniTask<bool>> condition, int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            if (await condition()) return true;
            await p.WaitSeconds(1f);
        }
        return await condition();
    }

    #endregion
});
