using System;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
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

        private float _processingPowerMultiplier;
        private ulong _processingPowerMultiplierTick = ulong.MaxValue;

        // 加工中の効果倍率はtick単位のスナップショット。同一tickの全読み手が同じ分母基準を見る
        // The processing multiplier is snapshotted per tick so every reader within a tick shares one basis
        public float ProcessingPowerMultiplier
        {
            get
            {
                if (_processingPowerMultiplierTick == GameUpdater.CurrentTick) return _processingPowerMultiplier;
                _processingPowerMultiplierTick = GameUpdater.CurrentTick;
                _processingPowerMultiplier = EffectComponent.AggregateCurrent().PowerMultiplier;
                return _processingPowerMultiplier;
            }
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

        // 選択レシピを保持し、入出力インベントリへスロット束縛をプッシュする
        // Store the selection and push the slot binding into the input/output inventories
        public void BindSelectedRecipe(MachineRecipeMasterElement recipe)
        {
            SelectedRecipe = recipe;
            InputInventory.SetBoundRecipe(recipe);
            OutputInventory.SetBoundRecipe(recipe);
        }
    }
}
