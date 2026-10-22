using Client.UitkPoc.Shell;
using UnityEngine.UIElements;

namespace Client.UitkPoc.TextAndInput
{
    public class TextAndInputDemo : IUitkPocDemo
    {
        private readonly UitkPocContext _context;

        public string Title => "文字と入力";

        public TextAndInputDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            return new Label("TODO");
        }
    }
}
