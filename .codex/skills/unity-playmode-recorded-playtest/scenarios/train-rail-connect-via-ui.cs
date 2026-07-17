// レール橋脚（TrainRail型ブロック）のUI経路設置・クリック結線検証。
// 橋脚2本をビルドメニュー→キーマウ操作で設置し、レールを手に持って橋脚Aクリック→橋脚Bクリックの順で
// クリック結線し、RailComponentのFront/BackNode同士が接続されることを検証する。
// 橋脚はデフォルト方向（北向き）のままZ軸沿いに設置し、回転操作なしで直線接続にする。
// Train rail pier (TrainRail block) UI-route placement + click-connect probe.
// Place two piers via build menu + key/mouse input, then hold the rail item and click pier A then pier B
// to connect them, verifying the RailComponent Front/Back nodes end up linked.
// Both piers keep the default direction (north) and are placed along the Z axis so no rotation is needed
// for a straight connection.
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
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
    p.WarpPlayer(new Vector3(11f, 33.5f, 10f));

    // レール橋脚をアンロックしホットバー0へ、接続用のレールをホットバー1へ用意する
    // Unlock the rail pier, stock hotbar slot 0 with piers and slot 1 with connecting rails
    p.UnlockBlock("レール橋脚");
    await p.GiveItemToHotbar(0, "レール橋脚", 5);
    await p.GiveItemToHotbar(1, "レール", 20);

    // 橋脚2本をZ軸沿いに設置する（デフォルト方向=北向きのまま直線接続できる配置）
    // Place two piers along the Z axis (default facing = north keeps the connection straight)
    var pierA = new Vector3Int(10, 32, 6);
    var pierB = new Vector3Int(10, 32, 14);

    await p.OpenBuildMenuAndSelectBlock("レール橋脚");
    await p.SelectHotbar(0);

    async UniTask PlacePier(Vector3Int origin)
    {
        await p.AimAtPlaceOrigin("レール橋脚", origin);
        await p.ClickPlace();
        await p.Until(() => p.GetBlock(origin) != null, 15f, $"橋脚設置反映 {origin}");
        await p.WaitBlockGameObject(origin);
    }

    await PlacePier(pierA);
    await PlacePier(pierB);
    await p.ExitToGameScreen();
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

    // チェーン手持ち結線と同じクリック結線パターン: レールを手に持ち、橋脚A→橋脚Bの順にクリック
    // Same click-to-connect pattern as the gear chain probe: hold the rail item, click pier A then pier B
    await p.OpenBuildMenuAndSelectBlock("レール橋脚");
    await p.SelectHotbar(1);

    await p.AimAt(aimA);
    await p.ClickPlace();
    await p.WaitSeconds(0.3f);
    await p.AimAt(aimB);
    await p.ClickPlace();

    await p.Until(AnyConnected, 15f, "クリック結線で2本のレールが接続された");
    await p.ExitToGameScreen();
    await p.Screenshot("02-connected");
});
