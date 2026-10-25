using System;
using System.IO;
using Core.Master;
using Microsoft.Extensions.DependencyInjection;
using Mod.Config;
using Mod.Loader;
using Server.Boot;
using UnityEngine;

namespace Server.MasterGate.Editor
{
    /// <summary>
    ///     ゲートの中で static のマスタ（MasterHolder）を書き換える唯一のクラス。読み込みに失敗したら、この Editor で最後に通ったマスタへ自分で戻す
    ///     The only class in the gates that rewrites the static master (MasterHolder); after a failed load it restores the last passing master of this Editor itself
    /// </summary>
    public static class ShippedMasterOwner
    {
        // この Editor で最後に読み込みが通った置き場。失敗した読み込みの後はここへ戻す（ドメインリロードで消える）
        // The location whose load last passed in this Editor; a failed load is followed by a reload of it (cleared by a domain reload)
        private static string _lastLoadedServerDataDirectory;

        // G3: 本番の起動と同じ DI 構築でサーバーを組む。Create の中でもう一度マスタを読むので、G1 が通った置き場でだけ組む
        // G3: build the server with the production DI container; Create reloads the master, so it only builds on a location whose G1 load passed
        public static bool TryCreateServer(string serverDataDirectory, out ServiceProvider serviceProvider, out string failure)
        {
            if (serverDataDirectory != _lastLoadedServerDataDirectory)
            {
                serviceProvider = null;
                failure = $"[ShippedMasterOwner] FAIL the server is built only on the directory whose G1 load last passed: {serverDataDirectory} (last passing: {_lastLoadedServerDataDirectory})";
                Debug.LogError(failure);
                return false;
            }

            (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(serverDataDirectory));
            failure = null;
            return true;
        }

        // G1: 出荷マスタを本番と同じ経路（ModsResource → MasterHolder.Load）で読み、ローダーと起動時検証が通るかを判定する
        // G1: load the shipped master through the production path (ModsResource -> MasterHolder.Load) and judge whether the loader and boot validation pass
        public static bool TryLoad(string serverDataDirectory, out string failure)
        {
            if (!TryLoadMaster(serverDataDirectory, out failure))
            {
                Debug.LogError(failure);
                RestoreLastLoaded(serverDataDirectory);
                return false;
            }

            _lastLoadedServerDataDirectory = serverDataDirectory;
            Debug.Log($"[ShippedMasterOwner] LOAD PASS {serverDataDirectory} research={MasterHolder.ResearchMaster.GetAllResearches().Count}");
            return true;
        }

        private static bool TryLoadMaster(string serverDataDirectory, out string failure)
        {
            // 置き場が無いときは推測で続けず、見た場所を理由に残す
            // When the location is missing, do not guess; record where we looked
            var modsDirectory = Path.Combine(serverDataDirectory, "mods");
            if (!Directory.Exists(modsDirectory))
            {
                failure = $"[ShippedMasterOwner] LOAD FAIL mods directory not found: {modsDirectory}";
                return false;
            }

            // 出荷マスタは別repoから来る外部入力JSON（境界）。必須キー欠落のローダー例外と検証例外をここで理由へ変え、呼び出し側がログに全文を残す
            // The shipped master is external-input JSON from another repo (a boundary); loader and validation exceptions become a reason here and the caller logs it in full
            try
            {
                MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(new ModsResource(modsDirectory))));
            }
            catch (Exception exception)
            {
                // 生成コードの MooresmasterLoaderException は ToString を上書きし型とスタックを落とすため、両方を明示して足す
                // The generated MooresmasterLoaderException overrides ToString and drops type and stack, so append both explicitly
                failure = $"[ShippedMasterOwner] LOAD FAIL MasterHolder.Load threw for {serverDataDirectory}:\n{exception.GetType().FullName}\n{exception}\nStackTrace:\n{exception.StackTrace}";
                return false;
            }

            failure = null;
            return true;
        }

        private static void RestoreLastLoaded(string failedServerDataDirectory)
        {
            // 壊れたマスタで読みかけた static を後続の dynamic code に残さないため、最後に通ったマスタを読み直す
            // Reload the last passing master so later dynamic code never runs on a half-loaded broken static master
            if (_lastLoadedServerDataDirectory == null)
            {
                Debug.LogWarning($"[ShippedMasterOwner] static master is half-loaded from {failedServerDataDirectory} and no earlier load passed in this Editor session; run G1 on a good master before anything reads MasterHolder");
                return;
            }

            if (TryLoadMaster(_lastLoadedServerDataDirectory, out var restoreFailure))
            {
                Debug.Log($"[ShippedMasterOwner] RESTORED static master from {_lastLoadedServerDataDirectory} after the failed load of {failedServerDataDirectory}");
                return;
            }

            // 戻す先も読めなくなった（ファイルが変わった）ので戻す先を捨て、理由を残す
            // The restore target no longer loads either (its files changed), so forget it and log why
            Debug.LogError($"[ShippedMasterOwner] FAIL could not restore the static master: {restoreFailure}");
            _lastLoadedServerDataDirectory = null;
        }
    }
}
