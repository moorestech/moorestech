using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    public class CleanRoomMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent
    {
        public Guid RecipeGuid => _processingState.RecipeGuid;
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
        private readonly float _idlePowerRate;

        private CleanRoomEffect _cleanRoomEffect = new(false, 0, 0);
        private ProcessState _lastState = ProcessState.Idle;

        public CleanRoomMachineProcessorComponent(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, float requestPower, float idlePowerRate, MachineModuleEffectComponent effect)
        {
            _context = new MachineProcessContext(input, output, effect, requestPower);
            _idlePowerRate = idlePowerRate;
            CurrentState = ProcessState.Idle;
            _processingState = new ProcessingMachineProcessState(_context, 0, null, null);

            _stateHandlers = new IMachineProcessState[]
                {
                    new IdleMachineProcessState(_context, _processingState),
                    _processingState,
                    new HaltedMachineProcessState(_processingState, () => _cleanRoomEffect.CanOperate),
                }.ToDictionary(handler => handler.State);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            var processingRate = Mathf.Clamp01(_processingState.TotalTicks > 0 ? 1f - (float)_processingState.RemainingTicks / _processingState.TotalTicks : 0f);
            var commonMachineBlock = CommonMachineBlockStateDetail.CreateState(_context.CurrentPower, _context.RequestPower, processingRate, CurrentState.ToStr(), _lastState.ToStr());
            var machineBlock = MachineBlockStateDetail.CreateState(processingRate, RecipeGuid);
            return new[] { commonMachineBlock, machineBlock };
        }

        public void SetCleanRoomEffect(CleanRoomEffect effect)
        {
            BlockException.CheckDestroy(this);
            _cleanRoomEffect = effect;
        }

        public void SupplyPower(float power)
        {
            BlockException.CheckDestroy(this);

            // 複数の電力セグメントから供給され得るため加算する
            // Accumulate because multiple energy segments may supply this machine
            _context.SuppliedPower += power;
            if (CurrentState == ProcessState.Idle) _changeState.OnNext(Unit.Default);
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

                _stateHandlers[current].OnExit();
                CurrentState = nextState;

                // HaltedからProcessingへ戻る時だけ入力再消費と残tick初期化を避ける
                // Only Halted-to-Processing skips re-entering Processing to avoid re-consuming inputs and resetting ticks
                if (current == ProcessState.Halted && nextState == ProcessState.Processing) return;
                _stateHandlers[nextState].OnEnter();
            }

            #endregion
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
