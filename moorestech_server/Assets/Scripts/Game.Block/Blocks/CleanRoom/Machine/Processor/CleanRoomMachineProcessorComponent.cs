using System;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Util;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.MachineRecipesModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.CleanRoom
{
    // VanillaMachineProcessorComponent をコピーして専用化。受信ゲートで開始/凍結を制御し、完了時に決定的 seed で抽選出力する。
    // Copied from VanillaMachineProcessorComponent; gates start/freeze on the receiver and emits via a deterministic cycle seed.
    public class CleanRoomMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent, ICleanRoomMachineRunningState
    {
        public Guid RecipeGuid => _processingRecipe?.MachineRecipeGuid ?? Guid.Empty;
        public float RequestPower => _requestPower;
        public ProcessState CurrentState { get; private set; }
        public uint RemainingTicks { get; private set; }

        // 加工中のみモジュールの電力倍率を適用した要求電力（Vanillaと同義）
        // Requested power with the module power multiplier applied only while processing (same as Vanilla)
        public float EffectiveRequestPower => _requestPower * (CurrentState == ProcessState.Processing ? _moduleEffect.AggregateCurrent().PowerMultiplier : 1f);

        // 稼働中（汚染中）判定。Processing 中は A_machine を部屋へ計上させる（凍結中も Processing 扱いだが部屋は Invalid なので影響しない）。
        // Running (polluting) when Processing; A_machine is booked while Processing (frozen stays Processing but the room is Invalid anyway).
        public bool IsRunning => CurrentState == ProcessState.Processing;

        // テスト可視カウンタ：出力インベントリから「出力なし（EUV失敗/Out）」通知を受けて加算する
        // Test-visible counter: incremented when the output inventory reports a no-output (EUV fail / Out)
        public int EuvFailCountForTest { get; private set; }

        public IObservable<Unit> OnChangeBlockState => _changeState;
        private readonly Subject<Unit> _changeState = new();

        private readonly VanillaMachineInputInventory _inputInventory;
        private readonly CleanRoomMachineOutputInventory _outputInventory;
        private readonly CleanRoomStateReceiverComponent _receiver;
        private readonly MachineModuleEffectComponent _moduleEffect;
        private readonly BlockInstanceId _blockInstanceId;
        private readonly float _requestPower;

        private MachineRecipeMasterElement _processingRecipe;
        private uint _processingRecipeTicks;
        private float _currentPower;
        private bool _usedPower;

        // セーブ対象：抽選の決定性をセーブ/ロード越しに保つためのサイクルカウンタ
        // Saved: deterministic-lottery cycle counter, persisted across save/load
        private uint _processedCycleCount;

        private ProcessState _lastState = ProcessState.Idle;

        // 新規作成
        // For new creation
        public CleanRoomMachineProcessorComponent(VanillaMachineInputInventory input, CleanRoomMachineOutputInventory output,
            CleanRoomStateReceiverComponent receiver, MachineModuleEffectComponent moduleEffect, BlockInstanceId blockInstanceId, float requestPower)
            : this(input, output, receiver, moduleEffect, blockInstanceId, requestPower, ProcessState.Idle, 0, null, 0)
        {
        }

        // セーブからの復元
        // For restoration from save
        public CleanRoomMachineProcessorComponent(VanillaMachineInputInventory input, CleanRoomMachineOutputInventory output,
            CleanRoomStateReceiverComponent receiver, MachineModuleEffectComponent moduleEffect, BlockInstanceId blockInstanceId, float requestPower,
            ProcessState currentState, uint remainingTicks, MachineRecipeMasterElement processingRecipe, uint processedCycleCount)
        {
            _inputInventory = input;
            _outputInventory = output;
            _receiver = receiver;
            _moduleEffect = moduleEffect;
            _blockInstanceId = blockInstanceId;
            _requestPower = requestPower;

            CurrentState = currentState;
            RemainingTicks = remainingTicks;
            _processingRecipe = processingRecipe;
            _processedCycleCount = processedCycleCount;
            if (processingRecipe != null) _processingRecipeTicks = _moduleEffect.AggregateCurrent().ScaleProcessingTicks(GameUpdater.SecondsToTicks(processingRecipe.Time));
        }

        public void SupplyPower(float power)
        {
            BlockException.CheckDestroy(this);
            _usedPower = false;
            _currentPower = power;

            // アイドル中の給電はクライアントに伝わらないため明示通知する
            // Idle-time power supply isn't reflected to the client, so notify explicitly
            if (CurrentState == ProcessState.Idle) _changeState.OnNext(Unit.Default);
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            if (_usedPower)
            {
                _usedPower = false;
                _currentPower = 0f;
            }

            // 状態ごとの処理
            // Per-state handling
            if (CurrentState == ProcessState.Idle) Idle();
            else Processing();

            // ステート変化時か処理中はイベントを発火
            // Fire the event on a state change or while processing
            if (_lastState != CurrentState || CurrentState == ProcessState.Processing)
            {
                _changeState.OnNext(Unit.Default);
                _lastState = CurrentState;
            }
        }

        private void Idle()
        {
            var isGetRecipe = _inputInventory.TryGetRecipeElement(out var recipe);

            // 受信値が InValidRoom=false（部屋外/Invalid/未プッシュ）なら開始しない
            // Do not start when the pushed effect says InValidRoom=false (outside / Invalid / not pushed)
            var roomAllowsStart = _receiver.CurrentEffect.InValidRoom;

            var isStartProcess = isGetRecipe && roomAllowsStart &&
                                 _inputInventory.IsAllowedToStartProcess() &&
                                 _outputInventory.IsAllowedToOutputItem(recipe);

            if (!isStartProcess) return;

            CurrentState = ProcessState.Processing;
            _processingRecipe = recipe;
            // モジュール速度効果を開始時に確定して適用（Vanillaと同じ）
            // Apply the module speed effect fixed at start (same as Vanilla)
            _processingRecipeTicks = _moduleEffect.AggregateCurrent().ScaleProcessingTicks(GameUpdater.SecondsToTicks(_processingRecipe.Time));
            _inputInventory.ReduceInputSlot(_processingRecipe);
            RemainingTicks = _processingRecipeTicks;
        }

        private void Processing()
        {
            // 室が無効化されたら進捗を凍結する（Idle に落とさない・入出力も壊さない）。
            // 凍結中も _usedPower=true＝供給電力は消費される（意図的な仕様）。
            // Freeze progress while the room is invalid (stay Processing; nothing breaks).
            // _usedPower stays true: supplied power IS consumed while frozen (intentional).
            if (!_receiver.CurrentEffect.InValidRoom)
            {
                _usedPower = true;
                return;
            }

            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_currentPower, EffectiveRequestPower);
            if (subTicks >= RemainingTicks)
            {
                RemainingTicks = 0;
                CurrentState = ProcessState.Idle;

                // サイクル完了：seed 用カウンタを前進させ、決定的 seed（blockInstanceId＋カウンタ）で出力を確定する
                // Cycle complete: advance the counter, then emit with the deterministic seed (blockInstanceId + counter)
                _processedCycleCount++;
                _outputInventory.InsertOutputSlot(_processingRecipe, ((long)_blockInstanceId.AsPrimitive() << 20) ^ _processedCycleCount);
            }
            else
            {
                RemainingTicks -= subTicks;
            }

            _usedPower = true;
        }

        // 出力インベントリが出力なし（EUV失敗/Out）を報告したときに加算する
        // Increment when the output inventory reports a no-output (EUV fail / Out)
        public void NotifyNoOutput()
        {
            EuvFailCountForTest++;
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            var processingRate = Mathf.Clamp01(_processingRecipeTicks > 0 ? 1f - (float)RemainingTicks / _processingRecipeTicks : 0f);
            return CleanRoomMachineProcessorStateDetail.Create(_currentPower, RequestPower, processingRate, CurrentState, _lastState, RecipeGuid);
        }

        // セーブデータ構築は DTO のファクトリへ委譲（_processedCycleCount を含め永続化）。
        // Save-data construction is delegated to the DTO factory (persists _processedCycleCount too).
        public CleanRoomMachineProcessorSaveJsonObject GetSaveJsonObject()
        {
            BlockException.CheckDestroy(this);
            return CleanRoomMachineProcessorSaveJsonObject.Create((int)CurrentState, GameUpdater.TicksToSeconds(RemainingTicks), RecipeGuid.ToString(), _processedCycleCount);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
