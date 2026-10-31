using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Inventory.Input;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Machine.State.Util;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.State
{
    // 加工ステート間で共有する状態と、電力の率導出・tickラッチを保持するクラス
    // Holds the state shared across processing states plus the power rate derivation and per-tick latching
    internal class MachineProcessContext
    {
        public readonly VanillaMachineInputInventory InputInventory;
        public readonly VanillaMachineOutputInventory OutputInventory;
        public readonly MachineModuleEffectComponent EffectComponent;
        public readonly float RequestPower;
        public readonly float IdlePowerRate;

        // プレイヤーが選択したレシピ。未選択はnullで、その間は加工しない
        // Recipe selected by the player; null means unselected and the machine never processes
        public MachineRecipeMasterElement SelectedRecipe { get; private set; }

        // このtickで各電力セグメントから供給された電力の加算器（次のUpdateでCurrentPowerへ確定）
        // Accumulator of power supplied by each energy segment this tick (latched into CurrentPower on the next Update)
        public float SuppliedPower;
        public float CurrentPower;

        // 分子CurrentPowerと同位置で確定するstate公開用の要求電力
        // Request power published to the state, latched at the same point as the numerator CurrentPower
        public float PublishedRequestPower { get; private set; }

        // 実現出力の差し替え口。清浄室のみが差し込み、通常機械はnullのまま
        // Realized-output rewrite hook; only the clean room installs one and the normal machine leaves it null
        private IRealizedOutputDecorator _realizedOutputDecorator;

        // 加工時間と同時に確定した電力倍率をジョブが設定する
        // The job sets the power multiplier fixed together with its processing time
        public float ProcessingPowerMultiplier { get; private set; }

        internal void SetProcessingPowerMultiplier(float powerMultiplier)
        {
            ProcessingPowerMultiplier = powerMultiplier;
        }

        public MachineProcessContext(
            VanillaMachineInputInventory inputInventory,
            VanillaMachineOutputInventory outputInventory,
            MachineModuleEffectComponent effectComponent,
            float requestPower,
            float idlePowerRate)
        {
            InputInventory = inputInventory;
            OutputInventory = outputInventory;
            EffectComponent = effectComponent;
            RequestPower = requestPower;
            IdlePowerRate = idlePowerRate;
        }

        // 稼働状態ごとの要求電力率。状態追加時にコンパイルではなく実行時例外で気付ける網羅switch
        // Requested power rate per state; the exhaustive switch makes a newly added state fail loudly
        public float EffectiveRequestPowerRate(ProcessState state)
        {
            return state switch
            {
                ProcessState.Halted => 0f,
                ProcessState.Processing => ProcessingPowerMultiplier,
                ProcessState.Idle => IdlePowerRate,
                // 出力詰まりは手が止まっているので待機と同じ率にする（満額要求のまま居座らせない）
                // Output blockage stands still, so it requests the idle rate instead of squatting on full power
                ProcessState.OutputBlocked => IdlePowerRate,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }

        public float EffectiveRequestPower(ProcessState state)
        {
            return RequestPower * EffectiveRequestPowerRate(state);
        }

        // 供給加算器を確定し、同じ地点・同じ状態基準で公開要求電力も確定する
        // Latch the supply accumulator and settle the published request power at the same point and state basis
        public void LatchTickPower(ProcessState state)
        {
            CurrentPower = SuppliedPower;
            SuppliedPower = 0f;
            PublishedRequestPower = EffectiveRequestPower(state);
        }

        // 状態を書き換えた直後に、供給加算器へ触れずに分母だけを取り直す
        // Re-latch only the denominator right after a state rewrite, leaving the supply accumulator untouched
        public void RelatchPublishedRequestPower(ProcessState state)
        {
            PublishedRequestPower = EffectiveRequestPower(state);
        }

        // 再通知が来ない停止状態では分子分母を0で固定し、古いスナップショットの固着を防ぐ
        // A halted machine gets no further notifications, so pin both sides to zero instead of leaving a stale snapshot
        public void PinPowerToZero()
        {
            CurrentPower = 0f;
            PublishedRequestPower = 0f;
        }

        // 選択レシピを保持し、入出力インベントリへスロット束縛をプッシュする。出力の許可集合は呼び出し側が組み立てて渡す（1フェーズ）
        // Store the selection and push the slot binding into the input/output inventories; the caller assembles the allowed output set (single phase)
        internal void BindSelectedRecipe(MachineRecipeMasterElement recipe, IReadOnlyList<IReadOnlyCollection<ItemId>> allowedOutputItemsPerSlot)
        {
            SelectedRecipe = recipe;
            InputInventory.SetBoundRecipe(recipe);
            OutputInventory.SetBoundOutputs(allowedOutputItemsPerSlot);
        }

        // 実現出力と加工時間を確定する唯一の口。容量判定も実挿入も加工時間も必ずこの戻り値を使うので、判定した物と挿入する物・時間の前提が食い違わない
        // The only place a job is realized; the capacity check, the real insert and the processing time all use this result, so none of them can diverge
        internal MachineRealization CreateRealization(MachineRecipeMasterElement recipe, int? inheritedLevel)
        {
            // 工場・差し替え口・加工時間が同じ集計を見るよう、モジュール効果は1回だけ集計し、セット数は工場の値を渡す
            // Aggregate the module effect once and pass the factory's set count so the factory, the rewrite hook and the time see the same values
            var effect = EffectComponent.AggregateCurrent();
            var outputs = MachineOutputFactoryUtil.CreateRealizedOutputs(recipe, effect, inheritedLevel, out var setCount);
            IRealizedOutputDrawRecord drawRecord = null;
            if (_realizedOutputDecorator != null) outputs = _realizedOutputDecorator.Decorate(recipe, inheritedLevel, effect, setCount, outputs, out drawRecord);
            return new MachineRealization(recipe, inheritedLevel, outputs, MachineRealization.ComputeTotalTicks(recipe, effect), effect.PowerMultiplier, drawRecord);
        }

        // 実現出力の差し替え口を差し込む（清浄室のチップ抽選）。未設定なら抽選結果をそのまま使う
        // Install the realized-output rewrite hook (clean-room chip draw); without one the rolled result is used as-is
        internal void SetRealizedOutputDecorator(IRealizedOutputDecorator decorator)
        {
            _realizedOutputDecorator = decorator;
        }
    }
}
