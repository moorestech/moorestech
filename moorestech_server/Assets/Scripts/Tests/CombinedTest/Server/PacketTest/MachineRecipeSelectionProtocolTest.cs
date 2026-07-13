using System;
using Core.Item;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.MachineRecipeSelectionProtocolTestHelper;

namespace Tests.CombinedTest.Server.PacketTest
{
    // SetRecipe/Clearと失敗系統を検証
    // Verifies the SetRecipe/Clear operations and failure paths of MachineRecipeSelectionProtocol
    public class MachineRecipeSelectionProtocolTest
    {
        private const int PlayerId = 1;
        private static readonly Vector3Int MachinePos = new(5, 0, 5);

        [Test]
        public void SetRecipeSelectsAndRespondsTest()
        {
            var (packet, _) = CreateServer();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var block = PlaceMachine(recipe.BlockGuid, MachinePos);

            var response = Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(MachinePos, recipe.MachineRecipeGuid, PlayerId));

            Assert.IsTrue(response.Success);
            Assert.AreEqual(MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.None, response.FailureReason);
            Assert.AreEqual(recipe.MachineRecipeGuid.ToString(), response.SelectedRecipeGuid);
            Assert.IsTrue(block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector));
            Assert.AreEqual(recipe.MachineRecipeGuid, selector.SelectedRecipeGuid);
        }

        [Test]
        public void ClearResetsSelectionTest()
        {
            var (packet, _) = CreateServer();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var block = PlaceMachine(recipe.BlockGuid, MachinePos);
            Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(MachinePos, recipe.MachineRecipeGuid, PlayerId));

            var response = Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateClearRequest(MachinePos, PlayerId));

            Assert.IsTrue(response.Success);
            Assert.AreEqual(MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.None, response.FailureReason);
            Assert.AreEqual(Guid.Empty.ToString(), response.SelectedRecipeGuid);
            Assert.IsTrue(block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector));
            Assert.AreEqual(Guid.Empty, selector.SelectedRecipeGuid);
        }

        [Test]
        public void WrongBlockRecipeIsRejectedTest()
        {
            var (packet, _) = CreateServer();
            var recipeA = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            MachineRecipeMasterElement recipeB = null;
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (r.BlockGuid == recipeA.BlockGuid) continue;
                recipeB = r;
                break;
            }
            Assert.IsNotNull(recipeB, "テストモッドに2種類以上の機械ブロックのレシピが必要");
            var block = PlaceMachine(recipeA.BlockGuid, MachinePos);

            var response = Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(MachinePos, recipeB.MachineRecipeGuid, PlayerId));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.RecipeBlockMismatch, response.FailureReason);
            Assert.IsTrue(block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector));
            Assert.AreEqual(Guid.Empty, selector.SelectedRecipeGuid);
        }

        [Test]
        public void LockedRecipeIsRejectedTest()
        {
            var (packet, _) = CreateServer();
            var lockedRecipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.LockedMachineRecipe);
            Assert.IsNotNull(lockedRecipe, "テストモッドにinitialUnlocked:falseのレシピが必要（Task 1）");
            var block = PlaceMachine(lockedRecipe.BlockGuid, MachinePos);

            var response = Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(MachinePos, lockedRecipe.MachineRecipeGuid, PlayerId));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.RecipeLocked, response.FailureReason);
            Assert.IsTrue(block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector));
            Assert.AreEqual(Guid.Empty, selector.SelectedRecipeGuid);
        }

        [Test]
        public void UnknownRecipeGuidIsRejectedTest()
        {
            var (packet, _) = CreateServer();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            PlaceMachine(recipe.BlockGuid, MachinePos);

            var response = Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(MachinePos, Guid.NewGuid(), PlayerId));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.InvalidRecipe, response.FailureReason);
        }

        [Test]
        public void NotMachineBlockIsRejectedTest()
        {
            var (packet, _) = CreateServer();
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, MachinePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];

            var response = Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(MachinePos, recipe.MachineRecipeGuid, PlayerId));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.NotMachine, response.FailureReason);
        }

        [Test]
        public void BlockNotFoundTest()
        {
            var (packet, _) = CreateServer();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];

            var response = Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(new Vector3Int(999, 0, 999), recipe.MachineRecipeGuid, PlayerId));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.BlockNotFound, response.FailureReason);
        }

        [Test]
        public void RefundFailedIsReportedTest()
        {
            var (packet, serviceProvider) = CreateServer();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var next = FindAlternateRecipe(recipe);
            Assert.IsNotNull(next, "テストモッドに代替レシピが必要");
            var block = PlaceMachine(recipe.BlockGuid, MachinePos);
            Assert.IsTrue(block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector));
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // レシピ選択済み・加工中の状態を作る
            // Build a state where the recipe is selected and processing is in progress
            Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(MachinePos, recipe.MachineRecipeGuid, PlayerId));
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
            GameUpdater.UpdateOneTick();
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            // 機械側を別アイテムで満杯にし塞ぐ
            // Fill the machine's own inventory with a different item so refunds have nowhere to land
            var machineFillerId = ForUnitTestItemId.ItemId3;
            var machineFillerMaxStack = ItemStackLevelDataStore.Instance.GetMaxStack(machineFillerId);
            for (var i = 0; i < blockInventory.GetSlotSize(); i++)
            {
                blockInventory.SetItem(i, machineFillerId, machineFillerMaxStack);
            }

            // プレイヤー側も満杯にし完全に塞ぐ
            // Fill the player's main inventory too, so there is no overflow destination left
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var playerFillerId = ForUnitTestItemId.ItemId4;
            var playerFillerMaxStack = ItemStackLevelDataStore.Instance.GetMaxStack(playerFillerId);
            for (var i = 0; i < mainInventory.GetSlotSize(); i++)
            {
                mainInventory.SetItem(i, ServerContext.ItemStackFactory.Create(playerFillerId, playerFillerMaxStack));
            }

            var response = Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(MachinePos, next.MachineRecipeGuid, PlayerId));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.RefundFailed, response.FailureReason);
            // 返却できないため変更自体が中止され、選択・加工状態は元のまま
            // The change is aborted since the refund cannot fit; selection and processing stay unchanged
            Assert.AreEqual(recipe.MachineRecipeGuid, selector.SelectedRecipeGuid);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
        }
    }
}
