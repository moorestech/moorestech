using System;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.MachineRecipesModule;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    // ProtocolTest共通のセットアップ/送信ヘルパー
    // Shared setup and send helpers for MachineRecipeSelectionProtocolTest
    internal static class MachineRecipeSelectionProtocolTestHelper
    {
        public static (PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        public static IBlock PlaceMachine(Guid blockGuid, Vector3Int position)
        {
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            return block;
        }

        public static MachineRecipeMasterElement FindAlternateRecipe(MachineRecipeMasterElement current)
        {
            foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (recipe.BlockGuid != current.BlockGuid) continue;
                if (recipe.MachineRecipeGuid == current.MachineRecipeGuid) continue;
                if (!recipe.InitialUnlocked) continue;
                return recipe;
            }
            return null;
        }

        public static MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse Send(
            PacketResponseCreator packet,
            MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest request)
        {
            var payload = MessagePackSerializer.Serialize(request);
            var responseBytes = packet.GetPacketResponse(payload, new PacketResponseContext(null))[0];
            return MessagePackSerializer.Deserialize<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse>(responseBytes);
        }
    }
}
