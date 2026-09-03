// ADR 0038: 初期装備・相対座標ゴースト・歯車接続の常設表示・鉱脈限定設置を通しで確認する
// ADR 0038: initial equipment, relative ghost, always-on gear connect lines, and vein-restricted placement, end to end
using System;
using System.IO;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Client.Game.InGame.Tutorial.TutorialBlock;
using Client.Playtest;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Gear.Common;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer;

// v8マスタの実GUID。原木鉱脈は開幕の木のチュートリアルが指す鉱脈
// Real GUIDs from the v8 master; the log vein is the one the opening tree tutorial points at
var logVeinGuid = new Guid("56ab3155-1479-49fa-a656-922021e4556a");
var windmillName = "燃料式風車";
var shaftName = "木のシャフト";
var crusherName = "原始的な粉砕機";
var minerName = "風力掘削機";
var stoneAxeName = "石の斧";

// 風車(3x3x3)のコネクタは原点+(2,0,2)で+xへ、シャフトはEast向きで±x、粉砕機のコネクタは原点+(0,0,0)で-xへ伸びる
// The windmill's connector at origin+(2,0,2) reaches +x, an East shaft reaches ±x, and the crusher's connector at origin reaches -x
var windmillOrigin = new Vector3Int(0, 32, 0);
var shaftOrigin = new Vector3Int(3, 32, 2);
var crusherOrigin = new Vector3Int(4, 32, 2);
var shaftOffsetFromWindmill = shaftOrigin - windmillOrigin;

var options = new PlaytestRunOptions { Record = true };

return PlaytestRunner.Run("placement-guided-tutorials", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { SpawnPosition = new Vector3(6f, 33.5f, -6f) });

    // 開幕スキットは出れば飛ばす。出ないワールドでもGameScreenへ到達すれば前提は満たされる
    // Skip the opening skit when it shows; a world without one still satisfies the precondition once GameScreen is reached
    p.Note("開幕スキットが出ていれば飛ばし、GameScreenへ入るのを待つ");
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    await p.Until(() =>
    {
        var current = skitStore.GetCurrent();
        if (current != null) skitStore.TrySkip(current.SessionId, current.SceneRevision);
        return p.CurrentUiState == Client.Game.InGame.UI.UIState.UIStateEnum.GameScreen;
    }, 120f, "GameScreen到達");

    // 1. 初期装備
    // 1. Initial equipment
    p.Note("石の斧が最初から装備スロットに入っているかを確認する");
    var playerInventory = p.ServerService<Game.PlayerInventory.Interface.IPlayerInventoryDataStore>().GetInventoryData(0);
    var stoneAxeId = MasterHolder.ItemMaster.GetItemId(new Guid("4c5fefbd-60a4-42ea-b70a-38a83b96e25e"));
    p.Assert(playerInventory.EquipmentInventory.GetItem(0).Id == stoneAxeId, $"{stoneAxeName}が装備スロット0へ初期装備として入っている");
    p.Assert(playerInventory.EquipmentInventory.GetSelectedItem().Id == stoneAxeId, $"{stoneAxeName}が選択済みの装備になっている");
    await p.Screenshot("01-initial-equipment");

    // 2. アンカーとなる燃料式風車をUI経路で置く
    // 2. Place the anchor windmill through the UI path
    p.Note("アンカーになる燃料式風車を置く");
    p.PlaceBlockDirect(windmillName, windmillOrigin, BlockDirection.North);
    await p.WaitBlockGameObject(windmillOrigin);
    await p.WaitSeconds(0.5f);

    // 3. 相対座標ゴースト
    // 3. Relative-coordinate ghost
    p.Note("相対座標チュートリアルを適用し、風車の原点+(3,0,2)にシャフトのゴーストが立つかを見る");
    var relativeManager = UnityEngine.Object.FindFirstObjectByType<RelativeBlockPlacePreviewTutorialManager>(FindObjectsInactive.Include);
    p.Assert(relativeManager != null, "RelativeBlockPlacePreviewTutorialManagerがMainGameシーンに結線されている");
    relativeManager.ApplyTutorial(BuildTutorial("relativeBlockPlacePreview", new JObject
    {
        ["anchorBlockGuid"] = BlockGuidOf(windmillName).ToString("D"),
        ["blockGuid"] = BlockGuidOf(shaftName).ToString("D"),
        ["offset"] = new JArray(shaftOffsetFromWindmill.x, shaftOffsetFromWindmill.y, shaftOffsetFromWindmill.z),
        ["blockDirection"] = "East",
        ["message"] = "風車の隣にシャフトを置いてください",
    }));

    TutorialBlockPreviewObject ghost = null;
    await p.Until(() =>
    {
        ghost = relativeManager.GetComponentInChildren<TutorialBlockPreviewObject>(false);
        return ghost != null;
    }, 30f, "相対座標ゴーストの生成");
    var shaftBlockId = MasterHolder.BlockMaster.GetBlockId(BlockGuidOf(shaftName));
    var expectedGhostPosition = Client.Game.InGame.BlockSystem.SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(shaftOrigin, BlockDirection.East, shaftBlockId);
    p.Assert(Vector3.Distance(ghost.transform.position, expectedGhostPosition) < 0.01f,
        $"ゴーストがアンカー原点+offset({shaftOrigin})に立った（実際 {ghost.transform.position} / 期待 {expectedGhostPosition}）");
    await p.Screenshot("02-relative-ghost");

    // 4. 歯車接続の常設表示
    // 4. Always-on gear connect lines
    p.Note("シャフトを持って設置モードに入り、噛み合う相手コネクタへ線が出るかを見る");
    await p.Hotbar.AssignHotbar(0, shaftName);
    await p.Hotbar.EnterBuildMode(0);
    await p.AimAtPlaceOrigin(shaftName, shaftOrigin);
    await p.WaitSeconds(0.5f);

    var gearPreviewRoot = GameObject.Find("GearConnectPreview");
    p.Assert(gearPreviewRoot != null, "歯車接続プレビューのルートが生成されている");
    System.Func<int> visibleGearLines = () => gearPreviewRoot == null
        ? 0
        : gearPreviewRoot.transform.Cast<Transform>().Count(child => child.gameObject.activeSelf);

    // 噛み合う向きが見つかるまでRで回す。線の有無が向きで変わること自体がこの表示の意味
    // Rotate with R until a meshing orientation appears; the lines changing with orientation is the point of this view
    var rotationCount = 0;
    while (rotationCount < 4 && visibleGearLines() == 0)
    {
        await p.PressKey(UnityEngine.InputSystem.Key.R);
        await p.WaitSeconds(0.5f);
        rotationCount++;
    }
    p.Assert(gearPreviewRoot != null && gearPreviewRoot.activeSelf, "設置プレビュー中に歯車接続線のルートが有効になっている");
    p.Assert(visibleGearLines() >= 1, $"風車と噛み合うコネクタへ線が引かれた（{visibleGearLines()}本 / R{rotationCount}回転）");
    await p.Screenshot("03-gear-connect-preview");

    // 5. ゴーストの座標へ置いてチュートリアルが完了することを確認する
    // 5. Place at the ghost cell and confirm the tutorial completes
    p.Note("ゴーストの座標へシャフトを設置し、チュートリアルが解除されるかを見る");
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(shaftOrigin) != null, 30f, "シャフトの設置反映");
    await p.WaitBlockGameObject(shaftOrigin);
    await p.Until(() => !relativeManager.IsApplied, 15f, "相対座標チュートリアルの完了");
    p.Assert(!relativeManager.IsApplied, "ゴースト座標への設置でチュートリアルが完了した");
    await p.Hotbar.ExitBuildMode(0);

    // 6. 風車→シャフト→粉砕機が1つの歯車ネットワークになることを確認する
    // 6. Confirm the windmill, shaft, and crusher end up in one gear network
    p.Note("粉砕機を並べ、風車からシャフト越しに歯車が繋がるかを確認する");
    p.PlaceBlockDirect(crusherName, crusherOrigin, BlockDirection.North);
    await p.WaitBlockGameObject(crusherOrigin);
    await p.WaitSeconds(0.5f);

    var gearNetworkDatastore = p.ServerService<GearNetworkDatastore>();
    var windmillBlock = p.GetBlock(windmillOrigin);
    var crusherBlock = p.GetBlock(crusherOrigin);
    p.Assert(windmillBlock != null && crusherBlock != null, "風車と粉砕機がサーバーに存在する");
    var windmillNetwork = gearNetworkDatastore.TryGetGearNetwork(windmillBlock.BlockInstanceId, out var windmillNet) ? windmillNet.NetworkId : (GearNetworkId?)null;
    var crusherNetwork = gearNetworkDatastore.TryGetGearNetwork(crusherBlock.BlockInstanceId, out var crusherNet) ? crusherNet.NetworkId : (GearNetworkId?)null;
    p.Assert(windmillNetwork != null && crusherNetwork != null, "風車と粉砕機がどちらも歯車ネットワークに属している");
    p.Assert(windmillNetwork != null && windmillNetwork.Equals(crusherNetwork), "風車・シャフト・粉砕機が同じ歯車ネットワークに入った");
    await p.Screenshot("04-gear-network");

    // 7. 鉱脈限定設置（対象鉱脈だけを強調する）
    // 7. Vein-restricted placement highlighting only the target vein
    p.Note("原木鉱脈だけを強調する設置制限をかけ、その鉱脈だけが緑で描かれるかを見る");
    var registry = ClientDIContext.DIContainer.DIContainerResolver.Resolve<MapVeinAabbRegistry>();
    p.Assert(registry.Veins.Count > 0, $"ワールドレイアウトに鉱脈がある（{registry.Veins.Count}件）");
    if (registry.Veins.Count == 0) throw new InvalidOperationException("the world layout has no vein at all");

    // 原木鉱脈があればそれを、無ければ最寄りの鉱脈を強調対象にする
    // Prefer the log vein when the world has one; otherwise highlight the nearest vein instead
    var logVeins = registry.Veins.Where(vein => vein.VeinTypeGuid == logVeinGuid).ToList();
    // 海中の露頭は絵に映らないので、同種のうち最も標高の高い1件を強調対象にする
    // A submerged vein would not show on camera, so highlight the highest one of its kind
    var targetVein = (logVeins.Count > 0 ? logVeins : registry.Veins.ToList())
        .OrderByDescending(vein => vein.Bounds.max.y).First();
    var targetVeinTypeGuid = targetVein.VeinTypeGuid;
    var targetVeinName = MasterHolder.MapVeinMaster.GetElementOrNull(targetVeinTypeGuid)?.VeinName;
    p.Note($"強調対象の鉱脈: {targetVeinName}（同種{logVeins.Count}件のうち最寄り）");

    var minerBlockId = MasterHolder.BlockMaster.GetBlockId(BlockGuidOf(minerName));
    var restrictionState = ClientDIContext.DIContainer.DIContainerResolver.Resolve<VeinRestrictedPlacementState>();
    restrictionState.SetRestriction(targetVeinTypeGuid, minerBlockId);
    p.Assert(restrictionState.IsRestrictedBlock(minerBlockId), "風力掘削機に対象鉱脈限定の制限がかかった");

    var veinCenter = targetVein.Bounds.center;
    // 範囲表示はカメラから96m以内の鉱脈を描くので、対象鉱脈の真上やや南へ立って画に収める
    // The range view draws veins within 96m of the camera, so stand just above and south of the target vein to frame it
    p.WarpPlayer(new Vector3(veinCenter.x, veinCenter.y + 2f, veinCenter.z - 8f));
    await p.WaitSeconds(0.5f);
    await p.Hotbar.AssignHotbar(1, minerName);
    await p.Hotbar.EnterBuildMode(1);
    await p.WaitSeconds(1f);

    var rangeViewRoot = GameObject.Find(MapVeinRangeViewService.RootObjectName);
    p.Assert(rangeViewRoot != null, "鉱脈範囲表示のルートが生成されている");
    var highlightedBoxes = rangeViewRoot == null
        ? 0
        : rangeViewRoot.transform.Cast<Transform>()
            .Count(child => child.gameObject.activeSelf &&
                            child.GetComponent<MeshRenderer>().sharedMaterial.name.Contains("Highlight"));
    var visibleBoxes = rangeViewRoot == null
        ? 0
        : rangeViewRoot.transform.Cast<Transform>().Count(child => child.gameObject.activeSelf);
    p.Assert(highlightedBoxes >= 1, $"対象の{targetVeinName}が強調マテリアルで描かれた（{highlightedBoxes}件）");
    p.Assert(visibleBoxes == highlightedBoxes, $"強調中は対象鉱脈以外が描かれない（表示{visibleBoxes}件・うち強調{highlightedBoxes}件）");

    // 制限は台帳のGUID絞り込みと底面XZ重なりで効く。鉱脈外セルは設置不可側になる
    // The restriction runs on the registry's per-GUID selection plus the footprint XZ overlap, so a cell outside the vein falls on the not-placeable side
    var insideCell = Vector3Int.FloorToInt(veinCenter);
    var outsideCell = insideCell + new Vector3Int(200, 0, 200);
    var targetVeins = registry.SelectVeinsOfType(targetVeinTypeGuid);
    p.Assert(targetVeins.Any(vein => new BlockPositionInfo(insideCell, BlockDirection.North, Vector3Int.one).OverlapsVeinXz(vein.MinCell, vein.MaxCell)), "鉱脈中心のセルは対象鉱脈の内側と判定される");
    p.Assert(!targetVeins.Any(vein => new BlockPositionInfo(outsideCell, BlockDirection.North, Vector3Int.one).OverlapsVeinXz(vein.MinCell, vein.MaxCell)), "遠く離れたセルは対象鉱脈の外側と判定される");
    await p.Screenshot("05-vein-restricted-highlight");

    p.Note("制限を解除し、鉱脈表示が種別表示へ戻ることを確認する");
    restrictionState.Clear();
    await p.WaitSeconds(0.5f);
    var afterClearHighlighted = rangeViewRoot == null
        ? 0
        : rangeViewRoot.transform.Cast<Transform>()
            .Count(child => child.gameObject.activeSelf &&
                            child.GetComponent<MeshRenderer>().sharedMaterial.name.Contains("Highlight"));
    p.Assert(afterClearHighlighted == 0, "制限解除で強調表示が消えた");
    await p.Screenshot("06-restriction-cleared");
    await p.Hotbar.ExitBuildMode(1);

    #region Internal

    // マスタの日本語名からブロックGUIDを引く
    // Look up a block GUID by its Japanese master name
    Guid BlockGuidOf(string blockName)
    {
        foreach (var blockId in MasterHolder.BlockMaster.GetBlockAllIds())
        {
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (master.Name == blockName) return master.BlockGuid;
        }
        throw new InvalidOperationException($"block not found in the live master: {blockName}");
    }

    // ライブのchallenges.jsonへチュートリアルを1件差し込み、生成型として取り出す
    // Insert one tutorial into the live challenges.json and take it back as the generated type
    TutorialsElement BuildTutorial(string tutorialType, JObject tutorialParam)
    {
        var challengesJson = JObject.Parse(File.ReadAllText(ChallengesMasterPath()));
        var challenge = (JObject)challengesJson["data"][0]["challenges"][0];
        var tutorials = (JArray)challenge["tutorials"];
        tutorials.Clear();
        tutorials.Add(new JObject
        {
            ["tutorialGuid"] = Guid.NewGuid().ToString("D"),
            ["tutorialType"] = tutorialType,
            ["tutorialParam"] = tutorialParam,
        });
        var challengeMaster = new ChallengeMaster(challengesJson);
        challengeMaster.Initialize();
        return challengeMaster.GetChallenge(Guid.Parse(challenge["challengeGuid"].Value<string>())).Tutorials[0];
    }

    string ChallengesMasterPath()
    {
        var found = Directory.GetFiles(Server.Boot.ServerDirectory.GetDirectory(), "challenges.json", SearchOption.AllDirectories);
        if (found.Length == 0) throw new InvalidOperationException("challenges.json was not found under the server directory");
        return found[0];
    }

    #endregion
});
