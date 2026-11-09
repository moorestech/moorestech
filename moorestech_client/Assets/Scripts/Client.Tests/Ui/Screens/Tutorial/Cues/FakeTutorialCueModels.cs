using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.Tutorial.KeyControlHint;
using Client.Ui.State.Tutorial.WorldPin;

namespace Client.Tests.Ui.Screens.Tutorial.Cues
{
    public sealed class FakeKeyControlHintModel : FakeModel<KeyControlHintViewModel>
    {
        public FakeKeyControlHintModel() : base(KeyControlHintViewModel.Empty) { }
    }

    public sealed class FakeWorldPinModel : FakeModel<WorldPinViewModel>
    {
        public FakeWorldPinModel() : base(WorldPinViewModel.Empty) { }
    }
}
