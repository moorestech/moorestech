using Game.Block.Interface.Component;
using System;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface.State;
using Game.Context;
using NUnit.Framework;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.MachineStateDetailsTestHelper;

namespace Tests.CombinedTest.Core.Machine
{
    internal static class MachineStatePowerTestHelper
    {
        public static void Verify(VanillaMachineProcessorComponent machineComponent, VanillaMachineBlockInventoryComponent inventoryComponent, IBlockStateObservable stateObservable, Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement recipe, float requiredPower)
        {
            // 電力供給なしでは加工が進まないことを確認
            Debug.Log("Testing without power supply...");
            // レシピ1回分の材料をすべて投入する（1種類だけでは加工条件を満たさず、以降の検証が空回りする）
            // Insert one full set of the recipe's inputs; a single item type never satisfies the start condition and would leave the checks below dead
            foreach (var inputItemInfo in recipe.InputItems)
            {
                inventoryComponent.InsertItem(ServerContext.ItemStackFactory.Create(inputItemInfo.ItemGuid, inputItemInfo.Count));
            }
            InsertRecipeFluids(recipe, inventoryComponent);
            // 電力供給なしでアップデート
            for (int i = 0; i < 5; i++)
            {
                GameUpdater.UpdateOneTick();
            }
            // 状態遷移自体は電力に依存しないため、進捗と電力が0で止まることを固定する
            // The state transition itself does not depend on power, so pin down that progress and power stay at zero
            var noPowerDetails = stateObservable.GetBlockStateDetails();
            var (_, noPowerCurrent, _, noPowerRate) = ExtractMachineDetails(noPowerDetails);
            Assert.AreEqual(0f, noPowerCurrent, 0.01f, "電力供給なしで電力が計上されています");
            Assert.AreEqual(0f, noPowerRate, 0.01f, "電力供給なしで加工が進んでいます");
            // 部分的な電力供給でも動作することを確認
            Debug.Log("Testing with partial power supply...");
            var reachedProcessing = false;
            var processingRateBefore = 0f;
            for (int i = 0; i < 20; i++)
            {
                machineComponent.SupplyExternalPower(requiredPower / 2);
                GameUpdater.UpdateOneTick();
                var details = stateObservable.GetBlockStateDetails();
                var (state, current, requested, rate) = ExtractMachineDetails(details);
                if (state != "processing") continue;
                Debug.Log($"Processing with partial power: Current={current}, Requested={requested}, Rate={rate}");
                Assert.AreEqual(requiredPower / 2, current, 1f, "部分的な電力供給が正しく反映されていません");
                // 分母は加工基準の実効要求電力（モジュール未装着なので基礎値）。期待値はマスタ値からの独立計算
                // The denominator is the processing-basis effective request power (the base value with no modules); the expectation comes from master values
                Assert.AreEqual(requiredPower, requested, 1f, "加工中の要求電力が実効値になっていません");
                Assert.Less(rate, 1f, "部分的な電力で最大速度になっています");
                reachedProcessing = true;
                processingRateBefore = rate;
                break;
            }
            // 加工へ入らないまま抜けるとアサートが1つも走らないため、到達自体を固定する
            // Pin down that processing was actually reached; otherwise none of the assertions above would run
            Assert.IsTrue(reachedProcessing, "部分的な電力供給で加工状態へ到達していません");
            // 半分の電力でも進捗は前進する（確率的丸めのため複数tickで判定する）
            // Half power still advances progress; judge over several ticks because the rounding is probabilistic
            for (int i = 0; i < 20; i++)
            {
                machineComponent.SupplyExternalPower(requiredPower / 2);
                GameUpdater.UpdateOneTick();
            }
            var (_, _, _, advancedRate) = ExtractMachineDetails(stateObservable.GetBlockStateDetails());
            Assert.Greater(advancedRate, processingRateBefore, "部分的な電力でも処理が進んでいません");
        }
    }
}
