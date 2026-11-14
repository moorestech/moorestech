// 計測の前にプレイヤーを決まった位置へ移し、その位置とプレビュー半径内のブロック数を返す（ベースラインワールドで設置モードが重い場所。2026-11-15 ユーザー指定）
// Move the player to the fixed measuring spot and return it with the count of blocks within the preview radius (a spot where build mode is heavy in the baseline world; chosen by the user on 2026-11-15)
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using UnityEngine;

var spot = new Vector3(622.21f, 82.99f, 420.90f);
PlayerSystemContainer.Instance.PlayerObjectController.SetPlayerPosition(spot);
ClientContext.VanillaApi.SendOnly.SendPlayerPosition(spot);

// 半径の判定は次の Tick で動くので、ここでは中心からの距離で数える（判定と同じ 20m。チャンク 4m 単位の判定とは端で少し違う）
// The radius evaluation runs on the next Tick, so count by distance here (same 20 m; differs slightly at the edge from the 4 m chunk evaluation)
var blocks = ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values;
var within = blocks.Count(b => Vector3.Distance(b.transform.position, spot) <= 20f);
return "player=" + PlayerSystemContainer.Instance.PlayerObjectController.Position.ToString("F2") + " blocksWithin20m=" + within;
