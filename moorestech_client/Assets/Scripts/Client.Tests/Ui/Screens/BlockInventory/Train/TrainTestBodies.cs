using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Train;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Platform.Edit;
using Client.Ui.State.Train.TimetableEdit;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 列車とプラットフォームの本文を、偽の源・偽の送信の上に本物の編集の器・Model・コマンドを本番どおり組んで作る（D-33）
    /// Builds the train and platform bodies with the real edit holders, models and commands over fake sources and fake senders, wired as in production (D-33)
    /// </summary>
    public sealed class TrainTestBodies
    {
        public FakeTrainSources Sources { get; } = new();
        public FakeTrainTimetableCommands TimetableCommands { get; } = new();
        public FakeTrainBlockCommands BlockCommands { get; } = new();
        public ReactiveProperty<BlockInventoryPanelState> Panel { get; }
        public TrainTimetableEditModel TimetableEdit { get; }
        public TrainTimetableEditCommands TimetableEditCommands { get; }
        public TrainPlatformEditModel PlatformEdit { get; }
        public TrainPlatformEditCommands PlatformEditCommands { get; }

        public TrainTestBodies(UiCommandResultToaster toaster, BlockInventoryPanelState panel)
        {
            // 本番と同じく器が源を一度だけ購読し、Model とコマンドは器を共有する
            // As in production the holders subscribe to their sources once, and models and commands share them
            Panel = new ReactiveProperty<BlockInventoryPanelState>(panel);
            var panels = new FakePanelSource(Panel);
            var timetableState = new TrainTimetableEditState(Sources, TimetableCommands, new ManualTrainTimetableWaitTimer(), toaster);
            TimetableEdit = new TrainTimetableEditModel(timetableState);
            TimetableEditCommands = new TrainTimetableEditCommands(timetableState, TimetableCommands, toaster);
            timetableState.Initialize();
            TimetableEdit.Initialize();
            var stationNameState = new TrainStationNameEditState(panels, Sources);
            PlatformEdit = new TrainPlatformEditModel(stationNameState);
            PlatformEditCommands = new TrainPlatformEditCommands(stationNameState, BlockCommands, toaster);
            stationNameState.Initialize();
        }

        public TrainInventoryBodyFactory TrainBodyFactory(BlockItemGridFactory grids, GameContentPresentation content)
        {
            return new TrainInventoryBodyFactory(Sources, TimetableEdit, TimetableEditCommands, Sources, TimetableCommands, grids, content);
        }

        public ITrainInventoryPart CreateTrainBody(BlockItemGridFactory grids, GameContentPresentation content)
        {
            return TrainBodyFactory(grids, content).Create();
        }

        public IBlockInventoryPart CreatePlatformBody(BlockItemGridFactory grids, GameContentPresentation content)
        {
            return new TrainPlatformBodyFactory(Sources, PlatformEdit, PlatformEditCommands, grids, content).Create(new BlockInventoryBodyMount(false));
        }
    }

    /// <summary>
    /// 本文に渡す枠の工場と表示素材。液体の絵は非 null の 1 枚にして、枠のアイコンの有無を読めるようにする
    /// Slot-grid factory and display material for the bodies; fluids get a non-null texture so the slot icon state is readable
    /// </summary>
    public static class BlockInventoryTestParts
    {
        // GameIcon は Texture が非 null のときだけ game-icon--image を立てる（前例 GrabOverlayViewTest の組み方）
        // GameIcon sets game-icon--image only for a non-null Texture (built like the GrabOverlayViewTest precedent)
        private static readonly Texture2D FluidTexture = new(4, 4) { hideFlags = HideFlags.DontSave };

        public static BlockItemGridFactory Grids()
        {
            return BlockInventoryTestBodies.Grids();
        }

        public static GameContentPresentation Content()
        {
            return new GameContentPresentation(new FakeContentTextureSource(null, null, FluidTexture));
        }
    }
}
