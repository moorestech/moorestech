// 新規ワールドと同一ワールド再起動で、同じ身元にID 1が割り当てられることを調べる
// Verify that a fresh world and a restart of that world assign ID 1 to the same identity
using System.IO;
using Client.Game.InGame.Context;
using Client.Network.API;
using Client.Playtest;
using Game.Paths;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("player-identity-sequential-id", options, async p =>
{
    var worldDirectory = p.ServerService<WorldDataDirectory>();
    var saveWasPresent = File.Exists(worldDirectory.SaveJsonFilePath);
    var phase = saveWasPresent ? "同一ワールド再開始" : "新規ワールド開始";
    p.Note($"{phase}: 接続由来のプレイヤーIDを確認する");

    // 接続設定とハンドシェイク応答が同じ連番を持つことを確かめる
    // Check that the connection setting and handshake response carry the same sequential ID
    var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
    var handshake = ClientDIContext.DIContainer.DIContainerResolver.Resolve<InitialHandshakeResponse>();
    p.Assert(playerId == 1, $"{phase}: PlayerId == 1 (actual={playerId})");
    p.Assert(handshake.PlayerId == playerId, $"{phase}: ハンドシェイクと接続設定のIDが一致する");

    await p.SkipOpeningSkitIfPlaying();
    await p.Screenshot(saveWasPresent ? "02-restarted-id-1" : "01-new-world-id-1");

    // AutoSave=falseの固定ワールド起動なので、次回の再開始用に書き出し完了まで待つ
    // Fixed-world boot disables autosave, so wait for a completed write before the next launch
    p.Note("次の起動で同じワールドを読むためセーブを確定する");
    var saveWaiter = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ServerSaveGenerationWaiter>();
    var saveResult = await saveWaiter.SaveAndWaitWrittenAsync(new ServerSaveGenerationWaiter.RealtimeBudget(60f));
    p.Assert(saveResult == ServerSaveGenerationWaiter.SaveWaitResult.Written, $"{phase}: セーブ書き出し完了 (actual={saveResult})");
    p.Assert(File.Exists(worldDirectory.SaveJsonFilePath), $"{phase}: save.json が存在する");
});
