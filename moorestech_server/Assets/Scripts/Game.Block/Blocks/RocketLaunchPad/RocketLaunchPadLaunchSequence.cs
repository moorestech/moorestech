using System;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad.Stage;
using Game.Block.Blocks.Util;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Orbit;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
namespace Game.Block.Blocks.RocketLaunchPad
{
    // 発射台の段階を1tickずつ進める。建造は成果が出力口に入りきるときだけ始め、打ち上げは ReadyToLaunch からだけ始める
    // Advances the pad stage one tick at a time; builds start only when outputs fit, launches only from ReadyToLaunch
    internal class RocketLaunchPadLaunchSequence
    {
        public RocketLaunchPadStage Stage { get; private set; } = IdleStage.Instance;
        public RocketLaunchPadWaitReason WaitReason { get; private set; }
        public float RequestPower => Stage switch
        {
            BuildingStage => _requiredPower,
            IdleStage or AwaitingBuildCommandStage or AwaitingLaunchCommandStage or ReadyToLaunchStage or LaunchingStage => 0f,
            _ => throw RejectUnknownStage(Stage),
        };
        private bool _reevaluateWait = true;
        private readonly RocketLaunchPadCargoLoader _cargoLoader;
        private readonly RocketLaunchPadOutputSlots _outputSlots;
        private readonly IOrbitalArrivalService _arrivalService;
        private readonly RocketLaunchHistory _launchHistory;
        private readonly float _requiredPower;
        private readonly uint _buildTotalTicks;
        private readonly uint _launchTotalTicks;
        public RocketLaunchPadLaunchSequence(RocketLaunchPadBlockParam param, RocketLaunchPadCargoLoader cargoLoader, RocketLaunchPadOutputSlots outputSlots, IOrbitalArrivalService arrivalService, RocketLaunchHistory launchHistory)
        {
            _cargoLoader = cargoLoader;
            _outputSlots = outputSlots;
            _arrivalService = arrivalService;
            _launchHistory = launchHistory;
            _requiredPower = param.RequiredPower;
            _buildTotalTicks = GameUpdater.SecondsToTicks(param.BuildSeconds);
            _launchTotalTicks = GameUpdater.SecondsToTicks(param.LaunchSeconds);
        }
        public void SetStage(RocketLaunchPadStage stage)
        {
            if (stage is not (IdleStage or AwaitingBuildCommandStage or BuildingStage or AwaitingLaunchCommandStage or ReadyToLaunchStage or LaunchingStage))
                throw RejectUnknownStage(stage);
            if (!ReferenceEquals(Stage, stage)) InvalidateWait();
            Stage = stage;
        }
        public void InvalidateWait() => _reevaluateWait = true;
        public void Advance(float tickPower)
        {
            // 発射の合図待ちは tick では進めない。合図か初打ち上げの通知でだけ段階が移る
            // The launch command wait does not advance by tick; only the command or the first-launch notice moves it
            if (Stage is IdleStage or AwaitingBuildCommandStage)
            {
                if (_reevaluateWait) { _reevaluateWait = false; AdvanceWaitingForCargo(); }
            }
            else if (Stage is BuildingStage building) AdvanceBuilding(building, tickPower);
            else if (Stage is ReadyToLaunchStage ready)
            {
                if (_reevaluateWait) { _reevaluateWait = false; AdvanceReadyToLaunch(ready); }
            }
            else if (Stage is LaunchingStage launching) AdvanceLaunching(launching);
            else if (Stage is AwaitingLaunchCommandStage) return; // silent-ok: 発射合図待ちは進行しない / UIに合図待ちを表示する
            else throw RejectUnknownStage(Stage);

            #region Internal

            void AdvanceWaitingForCargo()
            {
                // 初打ち上げ前だけ非消費で評価し、自動運転では選択と積載を一度に行う
                // Before the first launch evaluate without consumption; automatic operation selects and loads once
                WaitReason = _launchHistory.HasLaunchedOnce ? StartBuilding() : _cargoLoader.GetLoadWaitReason();
                if (WaitReason != RocketLaunchPadWaitReason.None) SetStage(IdleStage.Instance);
                else if (!_launchHistory.HasLaunchedOnce) SetStage(AwaitingBuildCommandStage.Instance);
            }

            void AdvanceBuilding(BuildingStage building, float tickPower)
            {
                // 電力の供給率だけ建造を進める
                // Advance the build by the power supply rate
                WaitReason = tickPower <= 0f ? RocketLaunchPadWaitReason.NoPower : RocketLaunchPadWaitReason.None;
                var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(tickPower, _requiredPower);
                if (subTicks < building.RemainingTicks)
                {
                    SetStage(new BuildingStage(building.Cargo, building.RemainingTicks - subTicks));
                    return;
                }

                // 初打ち上げ前は発射の合図を待ち、以後は打ち上げ待ちへ進む
                // Before the first launch wait for the launch command, otherwise move on to ready-to-launch
                WaitReason = RocketLaunchPadWaitReason.None;
                if (_launchHistory.HasLaunchedOnce) SetStage(new ReadyToLaunchStage(building.Cargo));
                else SetStage(new AwaitingLaunchCommandStage(building.Cargo));
            }

            void AdvanceReadyToLaunch(ReadyToLaunchStage ready)
            {
                // 打ち上げを始める唯一の口。成果が出力口に入りきらない間は待たせる
                // The only place a launch starts; wait while the outputs do not fit the output slots
                var payload = ready.Cargo.Payload;
                if (!_outputSlots.CanStore(_arrivalService.PreviewArrivalOutputs(payload.Id, payload.Count)))
                {
                    WaitReason = RocketLaunchPadWaitReason.OutputFull;
                    // silent-ok: 出力口の空き待ちは通常の待機 / プレイヤーは発射台 UI の待ち理由で気づく
                    return;
                }

                // 打ち上げ開始が軌道到着（CONTEXT.md）。成果を出力口へ入れ、ワールドの打ち上げを記録する
                // Launch start is orbital arrival (CONTEXT.md); store the outputs and record the world's launch
                WaitReason = RocketLaunchPadWaitReason.None;
                SetStage(new LaunchingStage(_launchTotalTicks));
                _outputSlots.Store(_arrivalService.Arrive(payload.Id, payload.Count));
                _launchHistory.RecordLaunch();
            }

            void AdvanceLaunching(LaunchingStage launching)
            {
                if (1 < launching.RemainingTicks) SetStage(new LaunchingStage(launching.RemainingTicks - 1));
                else SetStage(IdleStage.Instance);
            }

            #endregion
        }

        public RocketLaunchPadCommandResult RequestBuild()
        {
            if (Stage is not AwaitingBuildCommandStage) return RocketLaunchPadCommandResult.NotAwaitingBuildCommand;
            var reason = StartBuilding();
            if (reason == RocketLaunchPadWaitReason.OutputFull) return RocketLaunchPadCommandResult.OutputFull;
            if (reason == RocketLaunchPadWaitReason.MissingMaterials) return RocketLaunchPadCommandResult.MissingMaterials;
            if (reason == RocketLaunchPadWaitReason.MissingPayload) return RocketLaunchPadCommandResult.MissingPayload;
            return RocketLaunchPadCommandResult.Accepted;
        }

        public RocketLaunchPadCommandResult RequestLaunch()
        {
            // 合図は段階を移すだけ。打ち上げは次の Advance の AdvanceReadyToLaunch が行う
            // The command only moves the stage; the next Advance performs the launch in AdvanceReadyToLaunch
            if (Stage is not AwaitingLaunchCommandStage awaiting) return RocketLaunchPadCommandResult.NotAwaitingLaunchCommand;
            SetStage(new ReadyToLaunchStage(awaiting.Cargo));
            return RocketLaunchPadCommandResult.Accepted;
        }

        // 初打ち上げの後は発射の合図を待たない。段階を移すだけで、打ち上げは次の Advance が行う。建造の合図待ちは AdvanceWaitingForCargo が解く
        // No launch command wait after the first launch; only the stage moves and the next Advance launches. AdvanceWaitingForCargo releases the build wait
        public void ReleaseLaunchCommandWait()
        {
            InvalidateWait();
            if (Stage is AwaitingLaunchCommandStage awaiting) SetStage(new ReadyToLaunchStage(awaiting.Cargo));
        }

        public float CalculateProgress()
        {
            // 建造中・打ち上げ中は0〜1、完成したロケットが待つ段階は1。マスタ変更で残りが総量を超えても0〜1に収める
            // Building and launching report 0..1 and a finished waiting rocket reports 1; clamp even if remaining exceeds the total after a master change
            return Stage switch
            {
                BuildingStage building => Mathf.Clamp01(1f - building.RemainingTicks / (float)_buildTotalTicks),
                LaunchingStage launching => Mathf.Clamp01(1f - launching.RemainingTicks / (float)_launchTotalTicks),
                AwaitingLaunchCommandStage or ReadyToLaunchStage => 1f,
                IdleStage or AwaitingBuildCommandStage => 0f,
                _ => throw RejectUnknownStage(Stage),
            };
        }

        private RocketLaunchPadWaitReason StartBuilding()
        {
            if (!_cargoLoader.TryLoadNextCargo(out var cargo, out var reason)) return reason;
            SetStage(new BuildingStage(cargo, _buildTotalTicks));
            WaitReason = RocketLaunchPadWaitReason.None;
            return RocketLaunchPadWaitReason.None;
        }

        // 未対応の段階を待機扱いにせず、開発者へ理由を残して拒否する
        // Reject unsupported stages with a developer-visible reason instead of treating them as waiting
        private static InvalidOperationException RejectUnknownStage(RocketLaunchPadStage stage)
        {
            var message = $"発射台の未対応の段階を拒否しました 型:{stage.GetType().FullName}";
            Debug.LogError(message);
            return new InvalidOperationException(message);
        }
    }
}
