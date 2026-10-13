using Game.SaveLoad;
using Game.SaveLoad.Snapshot;
using Game.Train.Unit;

namespace Server.Boot
{
    // tick末尾の最終処理の順序を1箇所で明示する（MasterTickUpdaterの末尾版）。操作の適用まで終えた状態で、tickの境界→セーブ→常時記録スナップショットの順に回す
    // Declares the order of the final tick-end work in one place (the tick-end counterpart of MasterTickUpdater): with input already applied, run the tick boundary, then save, then the always-on snapshot
    // 3つは世界の状態を変えず互いに依存しないので順不同でも結果は同じ。読む人が迷わないよう順序をここで固定する
    // None of the three mutates world state or depends on another, so any order gives the same result; the order is fixed here so readers never have to wonder
    public class FinalTickEndUpdater
    {
        private readonly TrainUpdateService _trainUpdateService;
        private readonly WorldSaveCoordinator _worldSaveCoordinator;
        private readonly WorldSnapshotRing _worldSnapshotRing;

        public FinalTickEndUpdater(TrainUpdateService trainUpdateService, WorldSaveCoordinator worldSaveCoordinator, WorldSnapshotRing worldSnapshotRing)
        {
            _trainUpdateService = trainUpdateService;
            _worldSaveCoordinator = worldSaveCoordinator;
            _worldSnapshotRing = worldSnapshotRing;
        }

        public void Update()
        {
            // 境界: このtickの状態で列車・レール・ベルトのハッシュを出し、tick番号を進める。ベルトの組はまだ作り直されていないので搬送直後と同じハッシュになる
            // Boundary: emit the train, rail and belt hashes from this tick's state and advance the tick; the belt assembly is not rebuilt yet, so its hash equals the post-transport one
            _trainUpdateService.CompleteTick();

            // 全世界変更の確定後が唯一のセーブ可能な安定点（仕様2.1⑦）。境界と同じ状態を1枚で切り取る
            // The point after every world mutation commits is the only save-stable boundary (spec 2.1-7); it captures the same state as the boundary
            _worldSaveCoordinator.SaveIfRequested();

            // 常時記録のスナップショットはセーブと同じ安定点で取る（Startされるまで何もしない）
            // Always-on snapshots are captured at the same stable point as saves (inert until Start)
            _worldSnapshotRing.Update();
        }
    }
}
