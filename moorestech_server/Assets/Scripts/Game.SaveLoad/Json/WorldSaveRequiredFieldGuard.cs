using System;
using System.Collections.Generic;
using Game.SaveLoad.Json.WorldVersions;
using UnityEngine;

namespace Game.SaveLoad.Json
{
    // 現在版のセーブに必須の節が揃っているかを復元前に確かめる
    // Checks before restoring that a current-version save has every required section
    internal static class WorldSaveRequiredFieldGuard
    {
        // 版が古いセーブの欠損はマイグレーション連鎖が補うので、ここで補填はしない。欠けたまま進むと理由の無い素の例外になる
        // An older save's gaps are filled by the migration chain, not here; passing one through would fail with a reasonless bare exception
        internal static void ThrowIfMissing(WorldSaveAllInfo load)
        {
            var missing = new List<string>();
            if (!load.CurrentTick.HasValue) missing.Add("currentTick");
            if (load.RandomState == null) missing.Add("randomState");
            if (load.MiningCooldowns == null) missing.Add("miningCooldowns");
            if (load.BackfilledFields == null) missing.Add("backfilledFields");
            if (load.Players == null) missing.Add("players");
            if (load.SpaceElevatorCablePool == null) missing.Add("spaceElevatorCablePool");
            if (missing.Count == 0) return;

            var reason = $"セーブに {string.Join(" / ", missing)} がありません。版が古いセーブはマイグレーション連鎖（Game.SaveLoad/Migration）が補填するため、現在版のセーブで欠けているのは手編集による破損です";
            Debug.LogError(reason);
            throw new InvalidOperationException(reason);
        }
    }
}
