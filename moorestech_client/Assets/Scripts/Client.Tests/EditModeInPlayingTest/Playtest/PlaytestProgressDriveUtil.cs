using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Core.Master;
using Game.Block.Interface;
using Game.Challenge;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Research;
using Mooresmaster.Model.ChallengesModule;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest.Playtest
{
    // 進行記録の購読とプッシュを実起動で動かす操作。どれもサーバーから本人へイベントが届く経路（または本人の設置確定の送信口）で起こす
    // Drives the progress record's subscriptions and pushes on a real boot; each goes through the path that delivers the event to this player (or this player's placement send)
    public static class PlaytestProgressDriveUtil
    {
        // 建築モードの設置確定と同じ送信口を通す。サーバーへ直接置くと本人の設置として数えられない（F16）
        // Goes through the same send port as a build-mode confirmation; placing on the server directly would not count as this player's placement (F16)
        public static void PlaceBlockAsLocalPlayer(string blockName, Vector3Int position)
        {
            var placeInfo = new PlaceInfo { Position = position, Direction = BlockDirection.North, VerticalDirection = BlockVerticalDirection.Horizontal, BlockId = FindBlockId(), Placeable = true };
            Assert.IsTrue(PlaceBlockProtocolSender.SendPlaceBlockProtocol(new List<PlaceInfo> { placeInfo }), "設置確定が送信されていない");

            #region Internal

            BlockId FindBlockId()
            {
                foreach (var id in MasterHolder.BlockMaster.GetBlockAllIds())
                    if (MasterHolder.BlockMaster.GetBlockMaster(id).Name == blockName) return id;
                throw new ArgumentException($"Block not found: {blockName}");
            }

            #endregion
        }

        // 素材を本人のインベントリへ入れてからクライアントのクラフト要求を送る。成立したクラフトだけが本人へ届く
        // Puts the materials into this player's inventory, then sends the client's craft request; only a craft that goes through reaches this player
        public static Guid CraftFirstRecipe()
        {
            var recipe = MasterHolder.CraftRecipeMaster.CraftRecipes.Data[0];
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(ClientContext.PlayerConnectionSetting.PlayerId).MainOpenableInventory;
            foreach (var requiredItem in recipe.RequiredItems) inventory.InsertItem(ServerContext.ItemStackFactory.Create(requiredItem.ItemGuid, requiredItem.Count));

            ClientContext.VanillaApi.SendOnly.Craft(recipe.CraftRecipeGuid);
            return recipe.CraftRecipeGuid;
        }

        // 前提研究の無い未完了の研究を1つ、消費アイテムを揃えて完了させる。完了イベントは本人へ届く
        // Completes one unfinished research without prerequisites after supplying its consumed items; the completion event reaches this player
        public static Guid CompleteResearchWithoutPrerequisites()
        {
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            var researchDataStore = ServerContext.GetService<IResearchDataStore>();
            var states = researchDataStore.GetResearchNodeStates(playerId);
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

            foreach (var research in MasterHolder.ResearchMaster.GetAllResearches())
            {
                if (states[research.ResearchNodeGuid] == ResearchNodeState.Completed) continue;
                if (research.PrevResearchNodeGuids != null && research.PrevResearchNodeGuids.Length != 0) continue;

                foreach (var consumeItem in research.ConsumeItems) inventory.InsertItem(ServerContext.ItemStackFactory.Create(consumeItem.ItemGuid, consumeItem.ItemCount));
                if (researchDataStore.CompleteResearch(research.ResearchNodeGuid, playerId)) return research.ResearchNodeGuid;
            }

            Assert.Fail("前提研究の無い未完了の研究を完了させられなかった");
            return Guid.Empty;
        }

        // 現在のチャレンジの完了をサーバーのチャレンジイベントとして流す。クライアントへは実プレイと同じ完了パケットで届く
        // Emits the completion of a current challenge as the server's challenge event; the client receives the same completion packet as in real play
        public static Guid CompleteCurrentChallenge()
        {
            var currentChallenges = ServerContext.GetService<ChallengeDatastore>().CurrentChallengeInfo.CurrentChallenges;
            Assert.IsNotEmpty(currentChallenges, "新しいワールドに進行中のチャレンジが無い");

            var challenge = currentChallenges[0];
            ServerContext.GetService<ChallengeEvent>().InvokeCompleteChallenge(challenge, new List<ChallengeMasterElement>(), new List<string>());
            return challenge.ChallengeMasterElement.ChallengeGuid;
        }
    }
}
