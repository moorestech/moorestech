using System;
using System.Collections.Generic;
using Game.SaveLoad.Json.WorldVersions;
using UnityEngine;

namespace Game.SaveLoad.Json
{
    // 現在版のセーブに必須の節が欠けていれば理由付きで止める
    // Stops with a reason when a current-version save lacks a required section
    internal static class WorldSaveRequiredFieldGuard
    {
        internal static void ThrowIfMissing(WorldSaveAllInfo load)
        {
            // 値型も欠損を区別し、既定値で破損を隠さない
            // Distinguish absence even for value types instead of hiding corruption with defaults
            var missing = new List<string>();
            if (!load.CurrentTick.HasValue) missing.Add("currentTick");
            if (load.RandomState == null) missing.Add("randomState");
            if (load.MiningCooldowns == null) missing.Add("miningCooldowns");
            if (load.BackfilledFields == null) missing.Add("backfilledFields");
            if (load.Players == null) missing.Add("players");
            if (!load.RocketHasLaunchedOnce.HasValue) missing.Add("rocketHasLaunchedOnce");
            if (missing.Count == 0) return; // silent-ok: 必須項目が揃う正常経路 / ロード呼び出し元が完了を確認する

            // データストアを書き換える前に欠損名を残して中断する
            // Report missing fields and abort before mutating any datastore
            var reason = $"セーブに {string.Join(" / ", missing)} がありません。版が古いセーブはマイグレーション連鎖（Game.SaveLoad/Migration）が補填するため、現在版のセーブで欠けているのは手編集による破損です";
            Debug.LogError(reason);
            throw new InvalidOperationException(reason);
        }
    }
}
