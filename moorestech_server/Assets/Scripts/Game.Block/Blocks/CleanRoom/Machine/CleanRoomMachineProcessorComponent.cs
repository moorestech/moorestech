using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;
namespace Game.Block.Blocks.CleanRoom.Machine
{
    public class CleanRoomMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent, IBlockSaveState, IMachineRecipeSelectable
    {
        public Guid? RecipeGuid => _context.RecipeGuid;
        public float RequestPower => _context.RequestPower;
        public float CurrentPower => _context.CurrentPower;
        public ProcessState CurrentState { get; private set; }
        public bool IsPolluting => CurrentState == ProcessState.Processing;
        // 停止中は要求電力を0にし、稼働中だけ通常機械と同じ倍率を適用する
        // Halted machines request no power; operating states use the same multipliers as normal machines
        public float EffectiveRequestPower => CurrentState switch
        {
            ProcessState.Halted => 0f,
            ProcessState.Processing => _context.RequestPower * _context.EffectComponent.AggregateCurrent().PowerMultiplier,
            ProcessState.Idle => _context.RequestPower * _idlePowerRate,
            _ => throw new ArgumentOutOfRangeException(),
        };
        public IObservable<Unit> OnChangeBlockState => _changeState;
        private readonly Subject<Unit> _changeState = new();
        private readonly MachineProcessContext _context;
        private readonly Dictionary<ProcessState, IMachineProcessState> _stateHandlers;
        private readonly ProcessingMachineProcessState _processingState;
        private readonly VanillaMachineModuleInventory _moduleInventory;
        private readonly BlockInstanceId _blockInstanceId;
        private readonly float _idlePowerRate;
        private uint _cycleCount;
        private CleanRoomEffect _cleanRoomEffect = new(false, 0, 0);
        private ProcessState _lastState = ProcessState.Idle;
        public CleanRoomMachineProcessorComponent(Dictionary<string, string> componentStates, BlockInstanceId blockInstanceId, VanillaMachineInputInventory input, VanillaMachineOutputInventory output, VanillaMachineModuleInventory module, float requestPower, float idlePowerRate, MachineModuleEffectComponent effect)
        {
            _blockInstanceId = blockInstanceId;
            _moduleInventory = module;
            _idlePowerRate = idlePowerRate;
            CleanRoomMachineProcessorSaveState.Restore(componentStates, SaveKey, input, output, module, out var recipeGuid, out var restoredState,
                out var totalTicks, out var remainingTicks, out var pendingOutputs, out var pendingFluidOutputs, out var consumedItems, out _cycleCount);
            _context = new MachineProcessContext(input, output, effect, requestPower, recipeGuid);
            CurrentState = restoredState;
            _processingState = new ProcessingMachineProcessState(_context, totalTicks, remainingTicks, pendingOutputs, pendingFluidOutputs, consumedItems);
            _stateHandlers = new IMachineProcessState[]
                {
                    new IdleMachineProcessState(_context, _processingState),
                    _processingState,
                    new HaltedMachineProcessState(_processingState, () => _cleanRoomEffect.CanOperate),
                }.ToDictionary(handler => handler.State);
        }
        public MachineRecipeChangeResult TrySetRecipe(Guid? recipeGuid, IOpenableInventory playerMainInventory)
        {
            BlockException.CheckDestroy(this);
            var isChanged = recipeGuid != RecipeGuid;
            var result = _context.TrySetRecipe(recipeGuid, playerMainInventory, _processingState);
            if (result != MachineRecipeChangeResult.Success || !isChanged) return result;
            CurrentState = _cleanRoomEffect.CanOperate ? ProcessState.Idle : ProcessState.Halted;
            _changeState.OnNext(Unit.Default);
            return MachineRecipeChangeResult.Success;
        }
        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);
            var processingRate = Mathf.Clamp01(_processingState.TotalTicks > 0 ? 1f - (float)_processingState.RemainingTicks / _processingState.TotalTicks : 0f);
            var commonMachineBlock = CommonMachineBlockStateDetail.CreateState(_context.CurrentPower, _context.RequestPower, processingRate, CurrentState.ToStr(), _lastState.ToStr());
            var machineBlock = MachineBlockStateDetail.CreateState(processingRate, RecipeGuid);
            return new[] { commonMachineBlock, machineBlock };
        }
        public void SetCleanRoomEffect(CleanRoomEffect effect) { BlockException.CheckDestroy(this); _cleanRoomEffect = effect; }
        public void SupplyPower(float power)
        {
            BlockException.CheckDestroy(this);
            // 複数の電力セグメントから供給され得るため加算する
            // Accumulate because multiple energy segments may supply this machine
            _context.SuppliedPower += power;
            if (CurrentState == ProcessState.Idle) _changeState.OnNext(Unit.Default);
        }
        public string SaveKey { get; } = typeof(CleanRoomMachineProcessorComponent).FullName;
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var saveData = CleanRoomMachineProcessorSaveState.Build(_context.InputInventory, _context.OutputInventory, _moduleInventory, RecipeGuid, CurrentState, _processingState, _cycleCount);
            return JsonConvert.SerializeObject(saveData);
        }
        public void Update()
        {
            BlockException.CheckDestroy(this);
            // 直前tickの給電を確定してから清浄室条件で状態遷移を判断する
            // Latch the previous tick's power before evaluating clean-room gated transitions
            _context.CurrentPower = _context.SuppliedPower;
            _context.SuppliedPower = 0f;
            if (!_cleanRoomEffect.CanOperate && CurrentState != ProcessState.Halted)
            {
                ForceHaltedWithoutCompletingJob();
            }
            else
            {
                UpdateCurrentState();
            }
            // ステート変化時か処理中はイベントを発火させる
            // Fire the event on a state change or while processing
            if (_lastState != CurrentState || CurrentState == ProcessState.Processing)
            {
                _changeState.OnNext(Unit.Default);
                _lastState = CurrentState;
            }
            #region Internal
            void ForceHaltedWithoutCompletingJob()
            {
                // Processing.OnExitは出力払い出しを行うため、清浄室喪失時は呼ばずに凍結する
                // Processing.OnExit pays outputs, so a clean-room loss freezes without invoking it
                CurrentState = ProcessState.Halted;
                _stateHandlers[ProcessState.Halted].OnEnter();
            }
            void UpdateCurrentState()
            {
                var current = CurrentState;
                var nextState = _stateHandlers[current].GetNextUpdate();
                if (nextState == current) return;
                if (current == ProcessState.Processing && nextState == ProcessState.Idle) ApplyChipDrawOnCompletion();
                _stateHandlers[current].OnExit();
                CurrentState = nextState;
                // HaltedからProcessingへ戻る時だけ入力再消費と残tick初期化を避ける
                // Only Halted-to-Processing skips re-entering Processing to avoid re-consuming inputs and resetting ticks
                if (current == ProcessState.Halted && nextState == ProcessState.Processing) return;
                _stateHandlers[nextState].OnEnter();
            }
            void ApplyChipDrawOnCompletion()
            {
                // チップはレシピ出力の置き換えで個数は増えないため、開始時の容量判定は素の出力のままで有効
                // Chips only swap recipe outputs in place without increasing counts, so start-time capacity checks stay valid
                var recipeGuid = _context.RecipeGuid;
                if (!recipeGuid.HasValue) return;
                if (!MasterHolder.CleanRoomMaster.TryGetChipDraw(recipeGuid.Value, out var chipDraw)) return;
                // サイクル完了ごとにカウンタを進め、ブロック固有で再現可能なシードにする
                // Advance the counter each completed cycle and create a per-block reproducible seed
                _cycleCount++;
                var seed = ((long)_blockInstanceId.AsPrimitive() << 20) ^ _cycleCount;
                var pendingOutputs = _processingState.PendingOutputs;
                var replaced = new List<IItemStack>(pendingOutputs.Count);
                for (var i = 0; i < pendingOutputs.Count; i++) replaced.Add(DrawSlot(pendingOutputs[i], i));
                _processingState.ReplacePendingOutputs(replaced);
                IItemStack DrawSlot(IItemStack output, int outputIndex)
                {
                    foreach (var distribution in chipDraw.OutputDistributions)
                    {
                        if (MasterHolder.ItemMaster.GetItemId(distribution.OutputItemGuid) != output.Id) continue;
                        var levels = new List<(int level, double weight, ItemId chipItemId)>();
                        foreach (var level in distribution.Levels)
                        {
                            levels.Add((level.Level, level.Weight, MasterHolder.ItemMaster.GetItemId(level.ChipItemGuid)));
                        }
                        levels.Sort((a, b) => a.level.CompareTo(b.level));
                        var result = CleanRoomChipDraw.TryDraw(levels, _cleanRoomEffect.MaxChipLevel, _cleanRoomEffect.DownBinRate, chipDraw.EuvSuccessRate, seed, outputIndex, out var itemId);
                        return result == CleanRoomChipDraw.Result.Drawn
                            ? ServerContext.ItemStackFactory.Create(itemId, output.Count)
                            : ServerContext.ItemStackFactory.CreatEmpty();
                    }
                    // このレシピ出力に対応する抽選テーブルが無ければ素の出力のまま
                    // If no distribution matches this recipe output, leave it unchanged
                    return output;
                }
            }
            #endregion
        }
        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
