using Client.UitkPoc.Shell;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Motion
{
    public class MotionDemo : IUitkPocDemo
    {
        private readonly UitkPocContext _context;

        public string Title => "動き";

        public MotionDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            return new Label("TODO");
        }
    }
}
