using Client.UitkPoc.Shell;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Overlay
{
    public class OverlayDemo : IUitkPocDemo
    {
        private readonly UitkPocContext _context;

        public string Title => "重ね・操作";

        public OverlayDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            return new Label("TODO");
        }
    }
}
