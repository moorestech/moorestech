// 無料設置デバッグ検証: FreeBlockPlacementをONにすると、未解放かつ建設コストを1つも持たないブロックでも
// 本番の設置プロトコル(va:placeBlock)経由で設置できることをE2E録画で確認する。
// web UI移行でuGUIビルドメニューのスロット自動選択は駆動できないため、UIが送るのと同一のPlaceBlockパケットを直接送出し、
// 修正対象のサーバー PlaceBlockProtocol を実経路で叩く（サーバー直挿入 PlaceBlockDirect ではなくプロトコル経路である点が重要）
// Free-placement debug verification (recorded E2E): with FreeBlockPlacement ON, locked blocks with zero required items
// are placed through the real placement protocol (va:placeBlock). We send the exact PlaceBlock packet the UI would send
// (not the direct datastore insert), so the fixed server-side PlaceBlockProtocol is exercised end-to-end
using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Playtest;
using Client.Playtest.Operations;
using Common.Debug;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.UnlockState;
using Server.Protocol.PacketResponse;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("free-placement-locked-block", options, async p =>
{
    // ブロック列(x0..12, z2..6)の手前(低Z側)に立たせ、TPSカメラ(既定で+Z向き)が列を正面に捉えるようにする
    // Stand in front of the block row (low-Z side) so the TPS camera (default +Z facing) frames the blocks
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(6f, 33.5f, -8f));

    // 無料設置デバッグをON（サーバーの強制設置分岐を有効化）
    // Turn on the free-placement debug toggle (enables the server's forced-placement branch)
    DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
    p.Note("無料設置デバッグをONにした。未解放・在庫ゼロで設置できるか検証する");
    await p.Screenshot("00-before-placement");

    // 検証対象: いずれも未解放・建設コスト持ち。チェスト=非電気、石窯=電気(電線ゲート)、粉砕機=歯車機械
    // Targets: all locked with construction cost. Chest=non-electric, kiln=electric(wire gate), crusher=gear machine
    var unlockController = p.ServerService<IGameUnlockStateDataController>();
    // ブロックは大きい(3x2x4等)ため、フットプリントが重ならないよう十分に離して配置する
    // Blocks are large (3x2x4 etc.), so space them far apart to avoid footprint overlap
    var targets = new (string name, Guid guid, Vector3Int pos)[]
    {
        ("鉄のコンベアチェスト", Guid.Parse("ab1a47dd-94fe-4e62-a005-a8318fc304a9"), new Vector3Int(0, 32, 2)),
        ("石窯",               Guid.Parse("26096ad0-d355-4e2c-9e45-a5c5e91856e0"), new Vector3Int(5, 32, 2)),
        ("原始的な粉砕機",      Guid.Parse("e5a2b42a-3608-432e-a814-7dbd85095fb1"), new Vector3Int(10, 32, 2)),
    };

    // 前提を固める: 全対象が未解放であること
    // Pin the premise: every target is locked
    foreach (var t in targets)
    {
        var unlocked = unlockController.BlockUnlockStateInfos[t.guid].IsUnlocked;
        p.Assert(!unlocked, $"前提: {t.name} は未解放");
    }

    // 本番プロトコルで1つずつ設置し、都度ワールド反映を待って録画に映す
    // Place one at a time via the real protocol, waiting for world reflection each time so the video shows it
    foreach (var t in targets)
    {
        var blockId = MasterHolder.BlockMaster.GetBlockId(t.guid);
        p.Note($"{t.name}(未解放/在庫0)を無料設置プロトコルで送信 @ {t.pos}");

        var placeInfo = new PlaceInfo
        {
            Position = t.pos,
            Direction = BlockDirection.North,
            VerticalDirection = BlockVerticalDirection.Horizontal,
            BlockId = blockId,
            Placeable = true,
            CreateParams = Array.Empty<BlockCreateParam>(),
        };
        ClientContext.VanillaApi.SendOnly.PlaceBlock(new List<PlaceInfo> { placeInfo });

        // 設置がサーバーで成立しクライアントViewが出るまで待つ（修正前はnullのまま＝timeoutで失敗）
        // Wait until placement takes effect and the client view spawns (before the fix it stayed null = timeout)
        await p.Until(() => p.GetBlock(t.pos) != null, 20f, $"{t.name} が無料設置された @ {t.pos}");
        await p.WaitBlockGameObject(t.pos);
        await p.WaitSeconds(0.5f);
    }

    await p.Screenshot("01-locked-blocks-placed-free");

    // 建設コスト素材は一切渡していないので在庫は空のまま＝コスト非消費の証拠
    // No required items were ever granted, so stock stays empty = proof cost was not consumed
    p.Assert(p.CountItem("鉄インゴット") == 0, "鉄インゴット在庫は0のまま（コスト非消費）");
    foreach (var t in targets)
    {
        var placed = p.GetBlock(t.pos);
        p.Assert(placed != null && placed.BlockId == MasterHolder.BlockMaster.GetBlockId(t.guid), $"{t.name} が対象IDで設置されている");
    }
    p.Note("未解放ブロック4種がコスト無しで設置された。無料設置デバッグは機能している");
    await p.Screenshot("02-verified");
});
