// V2スナップショットから最大持ち物の旧プレイヤーを引き継ぐことを調べる
// Verify that the player with the most items is claimed from a V2 snapshot
using System;
using System.IO;
using Client.Game.InGame.Context;
using Client.Network.API;
using Client.Playtest;
using Core.Master;
using Game.Paths;
using Newtonsoft.Json.Linq;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("player-identity-v2-claim", options, async p =>
{
    var worldDirectory = p.ServerService<WorldDataDirectory>();
    var backupPath = worldDirectory.BackupSaveJsonPath(2);
    var logItemGuid = new Guid("aafce615-6c30-48c4-a29e-3c5b3266748f");
    p.Note("V2セーブの退避と、旧プレイヤーの持ち物の引き継ぎを確認する");

    // 版2の原本が退避され、想定したテスターの在庫であることを確かめる
    // Confirm the V2 original was archived and contains the expected tester inventory
    var hasBackup = File.Exists(backupPath);
    p.Assert(hasBackup, "V2スナップショットの原本が backup/2/save.json にある");
    if (!hasBackup) return;
    var original = JObject.Parse(File.ReadAllText(backupPath));
    p.Assert((int)original["worldVersion"] == 2, "退避した原本の worldVersion == 2");
    var oldPlayer = original["playerInventory"] as JArray;
    p.Assert(oldPlayer != null, "V2原本に playerInventory がある");
    if (oldPlayer == null) return;
    JObject oldInventory = null;
    foreach (var entry in oldPlayer)
    {
        if ((int)entry["PlayerId"] == 1623179277) oldInventory = (JObject)entry;
    }
    var originalLogs = 0;
    if (oldInventory != null)
    {
        foreach (var item in (JArray)oldInventory["MainInventoryItems"])
        {
            if (Guid.Parse((string)item["itemGuid"]) == logItemGuid) originalLogs += (int)item["count"];
        }
    }
    p.Assert(originalLogs == 612, $"V2原本の旧プレイヤーに原木が612個 (actual={originalLogs})");

    // 変換後は旧IDを使わず、新しい連番ID 2と在庫を接続した身元へ渡す
    // After migration, the connecting identity receives ID 2 and the preserved inventory
    var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
    var handshake = ClientDIContext.DIContainer.DIContainerResolver.Resolve<InitialHandshakeResponse>();
    p.Assert(playerId == 2, $"V2変換後の PlayerId == 2 (actual={playerId})");
    p.Assert(handshake.PlayerId == playerId, "V2変換後のハンドシェイクと接続設定のIDが一致する");
    p.Assert(MasterHolder.ItemMaster.GetItemMaster(logItemGuid).Name == "原木", "指定GUIDはマスタ上の原木である");
    p.Assert(p.CountItem("原木") == 612, $"ID 2のメイン在庫に原木が612個 (actual={p.CountItem("原木")})");

    await p.SkipOpeningSkitIfPlaying();
    await p.Screenshot("03-v2-claimed-id-2");
});
