using System;
using System.Collections.Generic;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Selection;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 機械の区画の Model の偽物。Set で値を差し替える
    /// Fake of the machine section model; swap values with Set
    /// </summary>
    public sealed class FakeMachineSectionModel : FakeModel<SectionContent<MachineSectionViewModel>>, IMachineSectionModel
    {
        public FakeMachineSectionModel() : base(SectionContent<MachineSectionViewModel>.Absent)
        {
        }
    }

    /// <summary>
    /// 選んだ GUID を覚えるコマンドの偽物
    /// Fake recipe-select command recording chosen GUIDs in order, for screen tests
    /// </summary>
    public sealed class RecordingRecipeSelectCommand : IMachineRecipeSelectCommand
    {
        private readonly List<Guid> _selected = new();

        public IReadOnlyList<Guid> Selected => _selected;

        public void Select(Guid recipeGuid)
        {
            _selected.Add(recipeGuid);
        }
    }
}
