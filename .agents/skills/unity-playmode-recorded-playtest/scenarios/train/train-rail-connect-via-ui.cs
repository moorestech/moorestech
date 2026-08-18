// レール橋脚（TrainRail型ブロック）のUI経路設置・クリック結線検証。
// 橋脚2本をホットバー→キーマウ操作で設置し、レール接続ツールをホットバーで保持して橋脚Aクリック→橋脚Bクリックの順で
// クリック結線し、RailComponentのFront/BackNode同士が接続されることを検証する。
// 橋脚はデフォルト方向（北向き）のままZ軸沿いに設置し、回転操作なしで直線接続にする。
// Train rail pier (TrainRail block) UI-route placement + click-connect probe.
// Place two piers via hotbar + key/mouse input, then hold the rail connect tool via the hotbar and click pier A then pier B
// to connect them, verifying the RailComponent Front/Back nodes end up linked.
// Both piers keep the default direction (north) and are placed along the Z axis so no rotation is needed
// for a straight connection.
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface.Extension;
using Game.Train.RailGraph;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("train-rail-connect-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    // カメラは北(+Z)を向くためプレイヤーを南に置き、橋脚は前方(高いZ)へ設置する
    // The camera faces north (+Z), so place the player to the south and the piers ahead (higher Z)
    p.WarpPlayer(new Vector3(10f, 33.5f, -3f));

    // 開幕スキット(Story)を表示中はホットバー入力が効かないためSkipインテントで飛ばしGameScreenへ抜ける
    // The opening skit (Story) blocks hotbar input, so skip it via the intent path and reach GameScreen
    await p.SkipOpeningSkit();

    // 橋脚の建設コストとレール接続ツールの消費素材（補強棒材・鉄板）を用意し、ホットバー0/1へ割当てる
    // Stock the pier's construction cost and the rail connect tool's materials (reinforced rod, iron plate), then assign to hotbar 0/1
    p.UnlockBlock("レール橋脚");
    p.Hotbar.UnlockConnectTool("レール");
    await p.GiveConstructionCost("レール橋脚", 5);
    await p.GiveItem("補強棒材", 64);
    await p.GiveItem("鉄板", 32);
    await p.Hotbar.AssignHotbar(0, "レール橋脚");
    await p.Hotbar.AssignHotbar(1, "レール");

    // 橋脚2本をZ軸沿いに設置する（デフォルト方向=北向きのまま直線接続できる配置）
    // Place two piers along the Z axis (default facing = north keeps the connection straight)
    var pierA = new Vector3Int(10, 32, 6);
    var pierB = new Vector3Int(10, 32, 14);

    await p.Hotbar.EnterBuildMode(0);

    async UniTask PlacePier(Vector3Int origin)
    {
        await p.AimAtPlaceOrigin("レール橋脚", origin);
        await p.ClickPlace();
        await p.Until(() => p.GetBlock(origin) != null, 15f, $"橋脚設置反映 {origin}");
        await p.WaitBlockGameObject(origin);
    }

    await PlacePier(pierA);
    await PlacePier(pierB);
    await p.Hotbar.ExitBuildMode(0);
    await p.Screenshot("01-piers-placed");

    // 接続前提: 2本は独立したRailComponent/ノードとして存在する
    // Pre-connection premise: the two piers hold independent RailComponents/nodes
    RailComponent RailOf(Vector3Int pos) => p.GetBlock(pos).GetComponent<RailComponent>();
    var railA = RailOf(pierA);
    var railB = RailOf(pierB);
    p.Assert(railA != null && railB != null, "両橋脚にRailComponentが生成されている");

    bool AnyConnected() =>
        railA.FrontNode.ConnectedNodes.Any(n => n.NodeGuid == railB.FrontNode.Guid || n.NodeGuid == railB.BackNode.Guid) ||
        railA.BackNode.ConnectedNodes.Any(n => n.NodeGuid == railB.FrontNode.Guid || n.NodeGuid == railB.BackNode.Guid);
    p.Assert(!AnyConnected(), "接続前は未接続（孤立設置の確認）");

    // 接続クリックが当たる面（front/back）を、実座標から相手に近い方を動的に選ぶ
    // Dynamically pick whichever collider (front/back) sits closer to the other pier by world distance
    Vector3 ClosestAreaCenter(Vector3Int selfPos, Vector3Int otherPos)
    {
        var blockObject = Client.Game.InGame.Context.ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(selfPos);
        var otherObject = Client.Game.InGame.Context.ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(otherPos);
        var otherCenter = otherObject.transform.position;
        var areas = blockObject.GetComponentsInChildren<TrainRailConnectAreaCollider>(true);
        var best = areas.OrderBy(a => Vector3.Distance(a.GetComponent<Collider>().bounds.center, otherCenter)).First();
        return best.GetComponent<Collider>().bounds.center;
    }

    var aimA = ClosestAreaCenter(pierA, pierB);
    var aimB = ClosestAreaCenter(pierB, pierA);

    // 歯車チェーン結線と同じクリック結線パターン: レール接続ツールをホットバーで保持し、橋脚A→橋脚Bの順にクリック
    // Same click-to-connect pattern as the gear chain probe: hold the rail connect tool via the hotbar, click pier A then pier B
    await p.Hotbar.EnterBuildMode(1);

    await p.AimAt(aimA);
    await p.ClickPlace();
    await p.WaitSeconds(0.3f);
    await p.AimAt(aimB);
    await p.ClickPlace();

    await p.Until(AnyConnected, 15f, "クリック結線で2本のレールが接続された");
    await p.Hotbar.ExitBuildMode(1);
    await p.Screenshot("02-connected");
});
