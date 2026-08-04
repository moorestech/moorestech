using Client.Game.Localization;
using Client.Localization;
using Core.Master;
using Game.Context;
using Mod.Loader;

namespace Client.Starter.Initialization
{
    /// <summary>
    /// マスタロード後のmod順とMaster原文を集めてローカライズ基盤へ渡す起動時の組み立て役
    /// mod CSV不正やmod順不整合は例外として上がるため、呼び出しは起動処理のtryブロック内側に置く
    /// Startup composer that gathers the master mod order and Master sources, then pushes them into the localization foundation
    /// Invalid mod CSV and mod-order mismatches surface as exceptions here, so callers must invoke it inside the startup try block
    /// </summary>
    internal static class GameDictionaryComposer
    {
        internal static void Run()
        {
            // DI登録済みコンテナからマスタと同じmod順を受け取る
            // Read the exact master mod order from the registered DI container
            var masterContainer = ServerContext.GetService<MasterJsonFileContainer>();
            Localize.MergeGameDictionaries(
                ServerContext.GetService<ModsResource>(),
                masterContainer.SortedModIds,
                MasterSourceTextCollector.Collect());
        }
    }
}
