using Client.UitkPoc.Shell;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Preview3D
{
    public class Preview3DDemo : IUitkPocDemo
    {
        private readonly UitkPocContext _context;

        public string Title => "3D";

        public Preview3DDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            return new Label("TODO");
        }
    }
}
