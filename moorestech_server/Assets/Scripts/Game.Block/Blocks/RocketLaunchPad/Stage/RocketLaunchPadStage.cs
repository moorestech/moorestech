using System;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.RocketLaunchPad.Stage
{
    // 発射台の段階。段階ごとに意味を持つ値だけを持ち、作った後は変えない
    // A launch pad stage; it holds only the values meaningful to that stage and never changes after creation
    public abstract class RocketLaunchPadStage
    {
        public abstract RocketLaunchPadPhase Phase { get; }
    }

    // ロケットを積んでいる段階。撤去すると中身が返る
    // Stages holding a loaded rocket; removing the pad refunds its contents
    public abstract class LoadedRocketStage : RocketLaunchPadStage
    {
        public readonly RocketCargo Cargo;

        protected LoadedRocketStage(RocketCargo cargo)
        {
            // 空のロケットを積んだ段階は作れない
            // A loaded stage cannot be created with an empty rocket
            if (!cargo.IsLoaded) throw new ArgumentException("積荷の無いロケットで積載中の段階を作ろうとしました", nameof(cargo));
            Cargo = cargo;
        }
    }

    // 残りtickで進む段階（建造中・打ち上げ中）。進行中かと残りtickはこの型で引く
    // Stages advancing by remaining ticks (building, launching); progress and remaining ticks are queried through this type
    public interface IProgressingStage
    {
        uint RemainingTicks { get; }
    }

    // 1便ぶんの資材と積荷、成果を入れる出力口の空きを待つ
    // Waits for one launch worth of materials and payload and for output space for the arrival outputs
    public sealed class IdleStage : RocketLaunchPadStage
    {
        public static readonly IdleStage Instance = new();
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.Idle;

        private IdleStage() { }
    }

    // 初打ち上げ前だけ: 揃った台がプレイヤーの建造の合図を待つ
    // Before the first launch only: a ready pad waits for the player's build command
    public sealed class AwaitingBuildCommandStage : RocketLaunchPadStage
    {
        public static readonly AwaitingBuildCommandStage Instance = new();
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.AwaitingBuildCommand;

        private AwaitingBuildCommandStage() { }
    }

    public sealed class BuildingStage : LoadedRocketStage, IProgressingStage
    {
        public uint RemainingTicks { get; }
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.Building;

        public BuildingStage(RocketCargo cargo, uint remainingTicks) : base(cargo)
        {
            // 残り0の建造中は作れない。建造が終わったら次の段階へ移るため
            // A building stage with zero ticks left cannot exist; a finished build moves to the next stage
            if (remainingTicks == 0) throw new ArgumentOutOfRangeException(nameof(remainingTicks), "建造中の段階は残りtickが1以上です");
            RemainingTicks = remainingTicks;
        }
    }

    // 初打ち上げ前だけ: 完成したロケットがプレイヤーの発射の合図を待つ
    // Before the first launch only: a finished rocket waits for the player's launch command
    public sealed class AwaitingLaunchCommandStage : LoadedRocketStage
    {
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.AwaitingLaunchCommand;

        public AwaitingLaunchCommandStage(RocketCargo cargo) : base(cargo) { }
    }

    // 打ち上げが許され、成果が出力口に入りきるのを待つ。打ち上げはこの段階からだけ始まる
    // Launch is permitted and waits until the outputs fit the output slots; launches start only from this stage
    public sealed class ReadyToLaunchStage : LoadedRocketStage
    {
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.ReadyToLaunch;

        public ReadyToLaunchStage(RocketCargo cargo) : base(cargo) { }
    }

    public sealed class LaunchingStage : RocketLaunchPadStage, IProgressingStage
    {
        public uint RemainingTicks { get; }
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.Launching;

        public LaunchingStage(uint remainingTicks)
        {
            if (remainingTicks == 0) throw new ArgumentOutOfRangeException(nameof(remainingTicks), "打ち上げ中の段階は残りtickが1以上です");
            RemainingTicks = remainingTicks;
        }
    }
}
