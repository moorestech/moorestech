// Step 6: ゲーム起動と同じ経路でセーブをフルロードし、成否を返す。
// 「変換成功」≠「ロード可能」。デシリアライズ単体ではなく必ずこれで検証する。
// 失敗時は例外メッセージで次の未対応形式が分かる -> Step 1 の列挙を補強する。
// 実行: uloop execute-dynamic-code --project-path ./moorestech_client --code "$(cat references/load_test.cs)"
using System;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot;
using Game.SaveLoad.Interface;
using Game.Context;

try
{
    // DefaultSaveJsonFilePath が save_1.json を指す。別番号なら options.saveJsonFilePath を差し替える。
    var options = new MoorestechServerDIContainerOptions(ServerDirectory.GetDirectory());
    var (packet, sp) = new MoorestechServerDIContainerGenerator().Create(options);
    sp.GetService<IWorldSaveDataLoader>().LoadOrInitialize();
    return "LOAD OK | blocks=" + ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count;
}
catch (Exception e)
{
    var inner = e.InnerException != null ? (" || INNER: " + e.InnerException.Message) : "";
    return "LOAD FAILED: " + e.Message + inner;
}
// 注意: これはエディタの ServerContext にワールドを載せる。検証後は Unity 再起動推奨。
