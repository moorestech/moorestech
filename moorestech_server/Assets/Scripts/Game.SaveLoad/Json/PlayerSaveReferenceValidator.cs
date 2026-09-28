using System;
using Game.Entity.Interface;
using Game.PlayerIdentity;
using Game.SaveLoad.Json.WorldVersions;
using UnityEngine;

namespace Game.SaveLoad.Json
{
    // プレイヤー状態が身元対応表の外へ孤立していないかロード前に検査する
    // Reject player state whose ID is absent from the identity registry before loading it
    internal static class PlayerSaveReferenceValidator
    {
        internal static void Validate(WorldSaveAllInfo save, IPlayerIdentityRegistry identities)
        {
            if (save.Inventory != null)
                foreach (var entry in save.Inventory) Check(entry.PlayerId, "playerInventory");
            if (save.Entities != null)
                foreach (var entry in save.Entities)
                    if (entry.Type == VanillaEntityType.VanillaPlayer) Check(entry.InstanceId, "entities");
            if (save.PlayerRidingStates != null)
                foreach (var entry in save.PlayerRidingStates) Check(entry.PlayerId, "playerRidingStates");
            if (save.HotbarAssignments != null)
                foreach (var entry in save.HotbarAssignments) Check(entry.PlayerId, "hotbarAssignments");
            if (save.RemainingPlacementCounts != null)
                foreach (var entry in save.RemainingPlacementCounts) Check(entry.PlayerId, "remainingPlacementCounts");
            if (save.ConstructionPayers != null)
                foreach (var entry in save.ConstructionPayers) Check(entry.PlayerId, "constructionPayers");
            if (save.MiningCooldowns != null)
                foreach (var entry in save.MiningCooldowns) Check(entry.PlayerId, "miningCooldowns");

            #region Internal

            void Check(long playerId, string section)
            {
                if (identities.IsRegisteredPlayerId(playerId)) return;
                var reason = $"セーブの {section} に身元対応表に無いプレイヤーID {playerId} があります";
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }

            #endregion
        }
    }
}
