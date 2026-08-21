// 歯車チェーンポールのセグメント構築検証（UI経路・複雑シナリオプローブ）:
// ポール5本をキーマウス操作のみで設置し、2本連結セグメントと3本連結セグメントを作る。
// ポール設置はホットバー割当駆動（設置対象IDを割当てた枠を建築モードで保持＝設置＋チェーン自動接続）。
// セグメントの分離は「同キーで建築モードを抜けて起点ポールをリセット」で行う。
// Gear chain pole segment probe (UI route, complex scenario):
// place 5 poles via key/mouse only, forming one 2-pole and one 3-pole chained segment.
// Pole placement is hotbar-assignment-driven (holding the assigned placement-target slot in build mode enables continuous extension = place + auto-chain).
// Segments are separated by exiting build mode with the same key, which resets the extension source pole.
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Gear.Common;
using Game.Block.Interface;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("gear-chain-pole-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    // カメラは北(+Z)を向くためプレイヤーを南に置く。X方向に広い5本編成のため中央かつ十分離れた位置に立つ
    // The camera faces north (+Z), so place the player to the south; stand centered and far enough back for the wide X spread of 5 poles
    p.WarpPlayer(new Vector3(12f, 33.5f, -10f));

    // 開幕スキット(Story)を表示中はホットバー入力が効かないためSkipインテントで飛ばしGameScreenへ抜ける
    // The opening skit (Story) blocks hotbar input, so skip it via the intent path and reach GameScreen
    await p.SkipOpeningSkit();

    // ポールの建設コストと、延長ごとに消費されるチェーン素材（鉄のワイヤー）の在庫を用意する
    // Stock the pole's construction cost and the chain material (iron wire) consumed by each extension
    p.UnlockBlock("歯車チェーンポール");
    p.Hotbar.UnlockConnectTool("歯車チェーン");
    await p.GiveConstructionCost("歯車チェーンポール", 10);
    await p.GiveItem("鉄のワイヤー", 100);   // give命令は1回=1スタックのため、maxStack(100)以内に収める

    // ポール割当後同キーで建築モードへ
    // Assign the pole to hotbar slot 1, then the same key enters build mode
    await p.Hotbar.AssignHotbar(0, "歯車チェーンポール");
    await p.Hotbar.EnterBuildMode(0);

    // ポール1本をクリック設置し、サーバー反映とクライアント出現（＝延長起点の確定）を待つ
    // Click-place one pole, then wait for server placement and client spawn (which fixes the extension source)
    async UniTask PlacePole(Vector3Int origin)
    {
        await p.AimAtPlaceOrigin("歯車チェーンポール", origin);
        await p.ClickPlace();
        await p.Until(() => p.GetBlock(origin) != null, 15f, $"ポール設置反映 {origin}");
        await p.WaitBlockGameObject(origin);
        await p.WaitSeconds(0.5f);
    }

    // セグメントA: 2本連結（A1設置→A2延長でチェーン自動接続）
    // Segment A: 2 poles (place A1, extend to A2 with auto-chain)
    var a1 = new Vector3Int(2, 32, 2);
    var a2 = new Vector3Int(6, 32, 2);
    await PlacePole(a1);
    await PlacePole(a2);

    // 同キーで建築モードを抜けて延長起点をリセットし、セグメントを分離する（遷移完了を待ってから次を打つ）
    // Exit build mode with the same key to reset the extension source and separate the segments (wait for the transition before the next tap)
    await p.Hotbar.ExitBuildMode(0);

    // セグメントB: 3本連結（割当は残っているので同キーで再入場できる）。Aと同じZ行でX方向に離す
    // Segment B: 3 poles (the assignment persists, so the same key re-enters); same Z row as A, offset along X
    var b1 = new Vector3Int(14, 32, 2);
    var b2 = new Vector3Int(18, 32, 2);
    var b3 = new Vector3Int(22, 32, 2);
    await p.Hotbar.EnterBuildMode(0);
    await PlacePole(b1);
    await PlacePole(b2);
    await PlacePole(b3);
    await p.Hotbar.ExitBuildMode(0);
    await p.Screenshot("01-poles-placed");

    // 歯車ネットワーク所属を検証: A1-A2同一 / B1-B2-B3同一 / AとBは別ネットワーク
    // Verify gear network membership: A1-A2 together, B1-B2-B3 together, A and B distinct
    var gearNetworkDatastore = p.ServerService<GearNetworkDatastore>();
    System.Func<Vector3Int, GearNetworkId?> networkOf = pos =>
    {
        var block = p.GetBlock(pos);
        if (block == null) return null;
        return gearNetworkDatastore.TryGetGearNetwork(block.BlockInstanceId, out var network) ? network.NetworkId : (GearNetworkId?)null;
    };

    p.Assert(networkOf(a1) != null, "A1がネットワークに所属");
    p.Assert(networkOf(a1) != null && networkOf(a1).Equals(networkOf(a2)), "A1-A2が同一ネットワーク（2本セグメント）");
    p.Assert(networkOf(b1) != null && networkOf(b1).Equals(networkOf(b2)) && networkOf(b2).Equals(networkOf(b3)), "B1-B2-B3が同一ネットワーク（3本セグメント）");
    p.Assert(networkOf(a1) != null && networkOf(b1) != null && !networkOf(a1).Equals(networkOf(b1)), "AとBは別ネットワーク（セグメント分離）");
    await p.Screenshot("02-verified");
});
