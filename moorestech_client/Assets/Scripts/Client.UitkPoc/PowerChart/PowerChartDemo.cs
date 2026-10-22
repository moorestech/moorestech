using Client.UitkPoc.Shell;
using UnityEngine.UIElements;

namespace Client.UitkPoc.PowerChart
{
    public class PowerChartDemo : IUitkPocDemo
    {
        private readonly UitkPocContext _context;

        public string Title => "電力グラフ";

        public PowerChartDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            return new Label("TODO");
        }
    }
}
