// 歯車チェーンポールのセグメント構築検証（UI経路・複雑シナリオプローブ）:
// ポール5本をキーマウス操作のみで設置し、2本連結セグメントと3本連結セグメントを作る。
// ポール設置はHoldingItemId駆動（ホットバーのポールアイテム所持で連続延長＝設置＋チェーン自動接続）。
// セグメントの分離は「GameScreenへ抜けて起点ポールをリセット」で行う。
// Gear chain pole segment probe (UI route, complex scenario):
// place 5 poles via key/mouse only, forming one 2-pole and one 3-pole chained segment.
// Pole placement is HoldingItemId-driven (holding the pole item enables continuous extension = place + auto-chain).
// Segments are separated by exiting to GameScreen, which resets the extension source pole.
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
    p.WarpPlayer(new Vector3(7f, 33.5f, 5f));

    // ポールはホットバー1（延長設置でアイテム消費）、チェーンはインベントリ在庫から自動消費される
    // Poles go to hotbar slot 1 (consumed by extension placement); chains are auto-consumed from inventory stock
    p.UnlockBlock("歯車チェーンポール");
    await p.GiveItemToHotbar(0, "歯車チェーンポール", 10);
    await p.GiveItem("歯車チェーン", 100);

    // ビルドメニューからポールを選択して設置モードへ入り、ホットバー1でポールを手持ちにする
    // Enter placement mode via the build menu, then hold the pole with hotbar key 1
    await p.OpenBuildMenuAndSelectBlock("歯車チェーンポール");
    await p.SelectHotbar(0);

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

    // GameScreenへ抜けて延長起点をリセットし、セグメントを分離する
    // Exit to GameScreen to reset the extension source and separate the segments
    await p.ExitToGameScreen();

    // セグメントB: 3本連結
    // Segment B: 3 poles
    var b1 = new Vector3Int(2, 32, 8);
    var b2 = new Vector3Int(6, 32, 8);
    var b3 = new Vector3Int(10, 32, 8);
    await p.OpenBuildMenuAndSelectBlock("歯車チェーンポール");
    await PlacePole(b1);
    await PlacePole(b2);
    await PlacePole(b3);
    await p.ExitToGameScreen();
    await p.Screenshot("01-poles-placed");

    // 歯車ネットワーク所属を検証: A1-A2同一 / B1-B2-B3同一 / AとBは別ネットワーク
    // Verify gear network membership: A1-A2 together, B1-B2-B3 together, A and B distinct
    var gearNetworks = p.ServerService<GearNetworkDatastore>().GearNetworks;
    System.Func<Vector3Int, GearNetworkId?> networkOf = pos =>
    {
        var block = p.GetBlock(pos);
        if (block == null) return null;
        foreach (var pair in gearNetworks)
        foreach (var transformer in pair.Value.GearTransformers)
        {
            if (transformer.BlockInstanceId == block.BlockInstanceId) return pair.Key;
        }
        return null;
    };

    p.Assert(networkOf(a1) != null, "A1がネットワークに所属");
    p.Assert(networkOf(a1) != null && networkOf(a1).Equals(networkOf(a2)), "A1-A2が同一ネットワーク（2本セグメント）");
    p.Assert(networkOf(b1) != null && networkOf(b1).Equals(networkOf(b2)) && networkOf(b2).Equals(networkOf(b3)), "B1-B2-B3が同一ネットワーク（3本セグメント）");
    p.Assert(networkOf(a1) != null && networkOf(b1) != null && !networkOf(a1).Equals(networkOf(b1)), "AとBは別ネットワーク（セグメント分離）");
    await p.Screenshot("02-verified");
});
