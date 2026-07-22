// チェーン手持ち結線モードの検証（UI経路）: 孤立ポール2本を設置し「別ネットワーク」を確認した後、
// 歯車チェーンを手に持ってポールA→ポールBの順にクリックし、結線されて同一ネットワークになることを検証する。
// 結線がキーマウ操作（ポールクリック→ChainConnectSend）から発火することの直接の証明。
// Chain-in-hand connect mode probe (UI route): place two isolated poles, confirm they are in different
// networks, then hold the gear chain item and click pole A then pole B to connect them.
// Directly proves the connection fires from mouse clicks (pole click -> ChainConnectSend), not a direct API call.
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Gear.Common;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("gear-chain-connect-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(5f, 33.5f, 3f));

    // ホットバー1=ポール、2=チェーン。ポールはブロック未解放だとビルドメニューに出ない
    // Hotbar 1 = poles, 2 = chains; the pole must be unlocked to appear in the build menu
    p.UnlockBlock("歯車チェーンポール");
    await p.GiveItemToHotbar(0, "歯車チェーンポール", 5);
    await p.GiveItemToHotbar(1, "歯車チェーン", 20);

    // 孤立ポール2本: 1本置くごとにGameScreenへ抜けて延長起点をリセットし、自動結線させない
    // Two isolated poles: exit to GameScreen after each to reset the extension source (no auto-chain)
    var c1 = new Vector3Int(2, 32, 2);
    var c2 = new Vector3Int(8, 32, 2);

    async UniTask PlaceIsolatedPole(Vector3Int origin)
    {
        await p.OpenBuildMenuAndSelectBlock("歯車チェーンポール");
        await p.SelectHotbar(0);
        await p.AimAtPlaceOrigin("歯車チェーンポール", origin);
        await p.ClickPlace();
        await p.Until(() => p.GetBlock(origin) != null, 15f, $"孤立ポール設置 {origin}");
        await p.WaitBlockGameObject(origin);
        await p.ExitToGameScreen();
    }

    await PlaceIsolatedPole(c1);
    await PlaceIsolatedPole(c2);

    // 結線前の前提を検証: 両ポールは別ネットワーク
    // Pre-connection premise: the two poles belong to different networks
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
    p.Assert(networkOf(c1) != null && networkOf(c2) != null && !networkOf(c1).Equals(networkOf(c2)), "結線前は別ネットワーク（孤立設置の確認）");
    await p.Screenshot("01-isolated-poles");

    // ポールの接続エリアコライダー中心を照準点にする（skillの定石: collider.bounds.center）
    // Aim at the pole's connect-area collider center (skill rule of thumb: collider.bounds.center)
    System.Func<Vector3Int, Vector3> poleClickPoint = pos =>
    {
        var blockObject = Client.Game.InGame.Context.ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos);
        var areaCollider = blockObject.GetComponentInChildren<GearChainPoleConnectAreaCollider>(true);
        return areaCollider.GetComponent<Collider>().bounds.center;
    };

    // チェーンを手に持ち設置モードへ（PlaceBlock stateはビルドメニュー経由で入る）
    // Hold the chain and enter placement mode (PlaceBlock state is entered via the build menu)
    await p.OpenBuildMenuAndSelectBlock("歯車チェーンポール");
    await p.SelectHotbar(1);

    // ポールA→ポールBの順にクリックして結線（A=起点選択、B=接続送信）
    // Click pole A then pole B to connect (A selects the source, B sends the connection)
    await p.AimAt(poleClickPoint(c1));
    await p.ClickPlace();
    await p.WaitSeconds(0.3f);
    await p.AimAt(poleClickPoint(c2));
    await p.ClickPlace();

    // 結線反映を条件待機し、同一ネットワークになったことを検証
    // Wait for the connection to land, then verify both poles share one network
    await p.Until(() => networkOf(c1) != null && networkOf(c1).Equals(networkOf(c2)), 15f, "クリック結線で同一ネットワーク化");
    await p.ExitToGameScreen();
    await p.Screenshot("02-connected");
});
