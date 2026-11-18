using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Family.Models;

namespace Client.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// 発電機の区画の工場
    /// Factory of the generator section (SectionStackSlot.Generator); shown when the model is present
    /// </summary>
    public sealed class GeneratorSectionFactory : ISectionStackSectionFactory
    {
        private readonly IGeneratorDetailModel _generator;

        public SectionStackSlot Slot => SectionStackSlot.Generator;

        public GeneratorSectionFactory(IGeneratorDetailModel generator)
        {
            _generator = generator;
        }

        public IBlockInventorySection Create(SectionStackSectionMount mount)
        {
            return new DetailSectionPresenter<GeneratorDetail>(_generator.Current, new GeneratorSectionView());
        }
    }
}
