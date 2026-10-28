using Client.Playtest.Operations;
using static Client.Playtest.Operations.PlaytestPlacementGuideChecks;
using Game.Context;
// ADR 0038: 初期装備・相対座標ゴースト・歯車接続の常設表示・鉱脈限定設置を通しで確認する
// ADR 0038: initial equipment, relative ghost, always-on gear connect lines, and vein-restricted placement, end to end
using System;
using System.IO;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.PreviewGhost;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util.AnchorRelative;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.PlacementGuide;
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
    var stoneAxeId = MasterHolder.ItemMaster.GetItemId(new Guid("4c5fefbd-60a4-42ea-b70a-38a83b96e25e"));
    var initialAxe = false;
    var selectedAxe = false;
    await p.RunOnServer("初期装備を読む", () =>
    {
        var equipment = ServerContext.GetService<Game.PlayerInventory.Interface.IPlayerInventoryDataStore>().GetInventoryData(0).EquipmentInventory;
        initialAxe = equipment.GetItem(0).Id == stoneAxeId;
        selectedAxe = equipment.GetSelectedItem().Id == stoneAxeId;
    });
    p.Assert(initialAxe, $"{stoneAxeName}が装備スロット0へ初期装備として入っている");
    p.Assert(selectedAxe, $"{stoneAxeName}が選択済みの装備になっている");
    await p.Screenshot("01-initial-equipment");

    // 2. アンカーとなる燃料式風車をUI経路で置く
    // 2. Place the anchor windmill through the UI path
    p.Note("アンカーになる燃料式風車を置く");
    await p.PlaceBlockDirect(windmillName, windmillOrigin, BlockDirection.North);
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

    // ゴースト実体は BlockPlacePreviewTutorialManager 配下に生成されるので、そちらから取る
    // The ghost instances are created under BlockPlacePreviewTutorialManager, so look there
    var ghostOwner = UnityEngine.Object.FindFirstObjectByType<BlockPlacePreviewTutorialManager>(FindObjectsInactive.Include);
    p.Assert(ghostOwner != null, "BlockPlacePreviewTutorialManagerがMainGameシーンに結線されている");

    PreviewGhostObject ghost = null;
    await p.Until(() =>
    {
        ghost = ghostOwner.GetComponentInChildren<PreviewGhostObject>(false);
        return ghost != null;
    }, 30f, "相対座標ゴーストの生成");

    // 期待セルと向きは本番と同じアンカー換算から出す。ローカルEastはアンカー向きで回る
    // The expected cell and direction come from the same anchor conversion as production; a local East rotates with the anchor
    var shaftBlockId = MasterHolder.BlockMaster.GetBlockId(BlockGuidOf(shaftName));
    var shaftBlockSize = MasterHolder.BlockMaster.GetBlockMaster(shaftBlockId).BlockSize;
    var anchorBlock = p.GetBlock(windmillOrigin);
    var expectedShaftOrigin = AnchorRelativeOriginUtil.ResolveWorldOrigin(anchorBlock.BlockPositionInfo, shaftOffsetFromWindmill, BlockDirection.East, shaftBlockSize);
    var expectedShaftDirection = AnchorRelativeDirectionUtil.RotateByAnchor(BlockDirection.East, anchorBlock.BlockPositionInfo.BlockDirection);
    p.Assert(expectedShaftOrigin == shaftOrigin, $"アンカー換算のセルが手計算と一致する（実際 {expectedShaftOrigin} / 期待 {shaftOrigin}）");

    var expectedGhostPosition = Client.Game.InGame.BlockSystem.SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(expectedShaftOrigin, expectedShaftDirection, shaftBlockId);
    p.Assert(Vector3.Distance(ghost.transform.position, expectedGhostPosition) < 0.01f,
        $"ゴーストがアンカー原点+offset({expectedShaftOrigin})に立った（実際 {ghost.transform.position} / 期待 {expectedGhostPosition}）");
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
    // 完了はエントリ解除でゴーストが破棄されることで観測する（IsAppliedは複数エントリ化で廃止済み）
    // Completion is observed as the ghost being torn down when the entry is released (IsApplied went away with multi-entry support)
    await p.Until(() => ghostOwner.GetComponentInChildren<PreviewGhostObject>(false) == null, 15f, "相対座標チュートリアルの完了");
    p.Assert(ghostOwner.GetComponentInChildren<PreviewGhostObject>(false) == null, "ゴースト座標への設置でチュートリアルが完了した");
    await p.Hotbar.ExitBuildMode(0);

    // 6. 風車→シャフト→粉砕機が1つの歯車ネットワークになることを確認する
    // 6. Confirm the windmill, shaft, and crusher end up in one gear network
    p.Note("粉砕機を並べ、風車からシャフト越しに歯車が繋がるかを確認する");
    await p.PlaceBlockDirect(crusherName, crusherOrigin, BlockDirection.North);
    await p.WaitBlockGameObject(crusherOrigin);
    await p.WaitSeconds(0.5f);

    var windmillBlock = p.GetBlock(windmillOrigin);
    var crusherBlock = p.GetBlock(crusherOrigin);
    p.Assert(windmillBlock != null && crusherBlock != null, "風車と粉砕機がサーバーに存在する");
    var networkIds = await p.ReadGearNetworks(new[] { windmillBlock, crusherBlock });
    var windmillNetwork = networkIds[0];
    var crusherNetwork = networkIds[1];
    p.Assert(windmillNetwork != null && crusherNetwork != null, "風車と粉砕機がどちらも歯車ネットワークに属している");
    p.Assert(windmillNetwork != null && windmillNetwork.Equals(crusherNetwork), "風車・シャフト・粉砕機が同じ歯車ネットワークに入った");
    await p.Screenshot("04-gear-network");

    await VerifyVeinRestriction(p, logVeinGuid, minerName);
});
