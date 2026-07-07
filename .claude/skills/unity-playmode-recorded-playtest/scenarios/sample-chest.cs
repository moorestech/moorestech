// サンプルシナリオ: 足場生成→チェスト設置→アイテム付与→検証→スクショ
// Sample scenario: scaffold -> place chest -> give item -> verify -> screenshot
// run-scenario.sh から execute-dynamic-code に渡されるスニペット
// Snippet passed to execute-dynamic-code by run-scenario.sh
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

var chestPosition = new Vector3Int(3, 32, 3);
var options = new PlaytestRunOptions { Record = false };

return PlaytestRunner.Run("sample-chest", options, async p =>
{
    await p.SetupFlatGround();
    p.PlaceBlockDirect("木のチェスト", chestPosition, BlockDirection.North);
    await p.WaitBlockGameObject(chestPosition);

    await p.GiveItem("鉄インゴット", 16);

    p.Assert(p.GetBlock(chestPosition) != null, "チェストがサーバーに存在する");
    p.Assert(p.CountItem("鉄インゴット") >= 16, "鉄インゴットが16個以上付与された");
    await p.Screenshot("final");
});
