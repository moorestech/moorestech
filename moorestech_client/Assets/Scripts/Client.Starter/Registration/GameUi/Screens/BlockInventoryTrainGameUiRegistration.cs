using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Train;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using Client.Ui.State.Train.TimetableEdit.Send;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    // 列車族のブロックインベントリ（列車の本文・駅/PF の本文）を登録する（S4-train）
    // Registers the train family block inventory: the train body and the station/platform body (S4-train)
    internal static class BlockInventoryTrainGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // 源の Model（源の購読は起動時に 1 回）。TrainTimetableModel は OpenTrainTimetableTracker（MainGameUiStateRegistration）より後に初期化され、開閉で取得状態が消えた後に読む
            // Source models subscribe once at startup; TrainTimetableModel initializes after OpenTrainTimetableTracker, so it reads fetch state after the tracker clears it on open/close
            builder.RegisterEntryPoint<TrainTimetableModel>().As<ITrainTimetableSource>();
            builder.RegisterEntryPoint<TrainInventoryBodyModel>().As<ITrainInventoryBodySource>();
            builder.RegisterEntryPoint<TrainPlatformBodyModel>().As<ITrainPlatformBodySource>();

            // 編集の器（Model とコマンドが同じ 1 つを型そのもので受ける。D-33）
            // Edit holders, resolved by their own type so the models and commands share one instance (D-33)
            builder.RegisterEntryPoint<TrainTimetableEditState>().AsSelf();
            builder.RegisterEntryPoint<TrainStationNameEditState>().AsSelf();

            // 描画値の Model（Presenter が購読）と、View が直接呼ぶコマンド（A-8）
            // Draw-value models the Presenters subscribe to, and commands the View calls directly (A-8)
            builder.RegisterEntryPoint<TrainTimetableEditModel>().AsSelf();
            builder.Register<TrainPlatformEditModel>(Lifetime.Singleton).AsSelf();
            builder.Register<TrainTimetableEditCommands>(Lifetime.Singleton).AsSelf();
            builder.Register<TrainPlatformEditCommands>(Lifetime.Singleton).AsSelf();

            // サーバーへ送るコマンドと時間切れ
            // Server-bound commands and the timeout
            builder.Register<TrainTimetableCommands>(Lifetime.Singleton).As<ITrainTimetableCommands>();
            builder.Register<TrainBlockCommands>(Lifetime.Singleton).As<ITrainBlockCommands>();
            builder.Register<UniRxTrainTimetableWaitTimer>(Lifetime.Singleton).As<ITrainTimetableWaitTimer>();

            // alpha の拡張口への差し込み
            // Plug into alpha's extension points
            builder.Register<TrainInventoryBodyFactory>(Lifetime.Singleton).As<ITrainInventoryBodyFactory>();
            builder.Register<TrainPlatformBodyFactory>(Lifetime.Singleton).As<IBlockInventoryBodyFactory>();
        }
    }
}
