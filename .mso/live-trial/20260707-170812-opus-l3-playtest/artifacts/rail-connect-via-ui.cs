// レール橋脚（TrainRail型ブロック）をUI経路で2本敷設し、結線ツール「レール」を手に持って
// 橋脚A→橋脚Bの接続コライダーを順にクリックし、両レールがレールグラフ上で接続されることを検証する。
// 設置はビルドメニュー経路（空手＝CommonBlockPlaceSystem）で行う。手持ちのレール橋脚を握る経路の
// TrainRailPlaceSystemはプレビューがBlockId=0で毎フレーム例外死するため設置不能（別途プロダクトバグ報告）。
// Lay two TrainRail "レール橋脚" piers via the build-menu route (empty hand => CommonBlockPlaceSystem),
// then hold the "レール" connect tool and click pier A's then pier B's collider to wire them,
// verifying the two rails become linked in the rail graph. (The hold-the-pier route is broken separately.)
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.Context;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Train.RailGraph;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("rail-connect-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(9.5f, 33.5f, 10f));          // 2本の橋脚の中間へ（トップダウン視界確保）

    // z軸に沿って直線に並ぶ2本の橋脚。直線＝ベジェ曲線が最も素直で結線が成立しやすい
    // Two piers collinear along z; a straight line is the most placeable bezier for connecting
    var posA = new Vector3Int(8, 32, 7);
    var posB = new Vector3Int(8, 32, 13);

    // ビルドメニュー設置の前提（解放＋RequiredItems付与）。結線ツール「レール」はスロット1へ（未選択のまま）
    // Prereq for build-menu placement (unlock + RequiredItems); the "レール" connect tool goes to slot 1 (unselected)
    await p.PrepareBlockForUiPlacement("レール橋脚", 4);
    await p.GiveItemToHotbar(1, "レール", 10);

    // 空手のままビルドメニューから橋脚を設置（HoldingItemId=空→CommonBlockPlaceSystem経路）
    // Place piers from the build menu with an empty hand (HoldingItemId empty => CommonBlockPlaceSystem)
    await p.PlaceBlockViaUi("レール橋脚", posA, BlockDirection.North);
    await p.PlaceBlockViaUi("レール橋脚", posB, BlockDirection.North);
    await p.WaitBlockGameObject(posA);
    await p.WaitBlockGameObject(posB);
    p.Assert(p.GetBlock(posA) != null && p.GetBlock(posB) != null, "橋脚2本がUI経路で設置される");
    await p.Screenshot("01-two-bare-piers");

    // レールグラフ上でAとBのノードが直接接続されているか（前後・両向き）を判定する
    // Whether A's and B's nodes are directly linked in the rail graph (either endpoint, either direction)
    var railA = p.GetBlock(posA).GetComponent<RailComponent>();
    var railB = p.GetBlock(posB).GetComponent<RailComponent>();
    System.Func<bool> connected = () =>
    {
        var aNodes = new HashSet<IRailNode> { railA.FrontNode, railA.BackNode };
        var bNodes = new HashSet<IRailNode> { railB.FrontNode, railB.BackNode };
        bool aToB = railA.FrontNode.ConnectedNodes.Any(n => bNodes.Contains(n)) || railA.BackNode.ConnectedNodes.Any(n => bNodes.Contains(n));
        bool bToA = railB.FrontNode.ConnectedNodes.Any(n => aNodes.Contains(n)) || railB.BackNode.ConnectedNodes.Any(n => aNodes.Contains(n));
        return aToB || bToA;
    };

    // 前提: 平置き直後は未接続（結線が明示操作から生じることの対照）
    // Premise: right after placement they are unconnected (baseline for the explicit connect)
    p.Assert(!connected(), "結線前は未接続（平置き橋脚は自動接続されない）");

    // 相手側を向いた接続コライダーの中心を照準点にする（A=より大きいz側、B=より小さいz側）
    // Aim at the connect collider facing the other pier (A = larger-z side, B = smaller-z side)
    System.Func<Vector3Int, bool, Vector3> facingColliderCenter = (pos, pickHigherZ) =>
    {
        var blockObject = ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos);
        var areas = blockObject.GetComponentsInChildren<TrainRailConnectAreaCollider>(true);
        var chosen = pickHigherZ
            ? areas.OrderByDescending(a => a.transform.position.z).First()
            : areas.OrderBy(a => a.transform.position.z).First();
        return chosen.GetComponent<Collider>().bounds.center;
    };

    // 結線ツール「レール」を手に持つ（HoldingItemId駆動でTrainRailConnectSystemへ切替、接続元リセット）
    // Hold the "レール" tool (HoldingItemId switches to TrainRailConnectSystem, source reset on Enable)
    await p.SelectHotbar(1);
    await p.WaitSeconds(0.3f);

    // クリック1: 橋脚Aの接続コライダー＝接続元を選択（GetKeyDownで確定）
    // Click 1: pier A's connect collider selects the FROM node (fires on GetKeyDown)
    await p.AimAt(facingColliderCenter(posA, true));
    await p.ClickPlace();
    await p.WaitSeconds(0.3f);

    // クリック2: 橋脚Bの接続コライダー＝接続先。ConnectRailが送信され両ノードが結線される
    // Click 2: pier B's connect collider is the TO node; ConnectRail is sent and the nodes are linked
    await p.AimAt(facingColliderCenter(posB, false));
    await p.ClickPlace();

    // 結線反映を条件待機し、レールグラフ上で接続されたことを検証
    // Wait for the connection to land, then verify the two rails are linked in the rail graph
    await p.Until(connected, 15f, "クリック結線で2本のレールが接続される");
    p.Assert(connected(), "結線後は接続状態（UI経路のクリック結線で成立）");

    await p.ExitToGameScreen();
    await p.Screenshot("02-rails-connected");
});
