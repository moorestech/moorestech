using Client.UitkPoc.Shell;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Effects
{
    public class EffectsDemo : IUitkPocDemo
    {
        private readonly UitkPocContext _context;

        public string Title => "表現";

        public EffectsDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            return new Label("TODO");
        }
    }
}
