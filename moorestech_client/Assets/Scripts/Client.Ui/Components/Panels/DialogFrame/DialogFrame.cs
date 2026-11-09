using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// モーダルの枠（題の行・本文・操作）
    /// Modal frame (title row, body, actions)
    /// </summary>
    /// <remarks>
    /// 幅は Mantine の --modal-size（md 440px）。Web の w={320} は content の flex-basis に負けて効かない。画面は祖先で --modal-size を上書きする
    /// Width is Mantine's --modal-size (md 440px); the Web w={320} loses to the content flex-basis. Screens override --modal-size on an ancestor
    /// </remarks>
    public sealed class DialogFrame : VisualElement
    {
        private readonly string _testid;
        private readonly VisualElement _surface;
        private readonly VisualElement _content;
        private readonly VisualElement _actions;
        private LocalizedLabel _title;

        public override VisualElement contentContainer => _content;

        public DialogFrame(string testid)
        {
            _testid = testid;
            name = testid;
            AddToClassList("dialog-frame");
            UiSurface.Mark(this);

            // 角丸で中身を切る面と、幅だけを持つ外側の箱を分ける（外側は切らないので、影を面の奥に置いても枠の外へ出せる）
            // Separate the rounded clipping surface from the width-only outer box (the outer box does not clip, so a shadow behind the surface can extend outside)
            hierarchy.Add(new DialogFrameShadow());
            _surface = new VisualElement();
            _surface.AddToClassList("dialog-frame__surface");
            hierarchy.Add(_surface);

            // 本文に中身と操作を縦に積む
            // Stack contents and actions vertically in the body
            var body = new VisualElement();
            body.AddToClassList("dialog-frame__body");
            _content = new VisualElement();
            _content.AddToClassList("dialog-frame__content");
            _actions = new VisualElement();
            _actions.AddToClassList("dialog-frame__actions");
            body.hierarchy.Add(_content);
            body.hierarchy.Add(_actions);
            _surface.Add(body);
        }

        // 題の行は最初の題で作る（題の無い枠は Mantine と同じく題の行を出さない）
        // The title row is created on the first title (an untitled frame shows no row, as in Mantine)
        public void SetTitle(UiText title)
        {
            if (_title == null) _title = CreateTitleRow();
            _title.SetText(title);

            #region Internal

            LocalizedLabel CreateTitleRow()
            {
                var header = new VisualElement();
                header.AddToClassList("dialog-frame__header");
                var created = new LocalizedLabel { name = $"{_testid}-title" };
                created.AddToClassList(UiTypographyClass.TitleOrder4);
                created.AddToClassList("dialog-frame__title");
                header.Add(created);
                _surface.Insert(0, header);
                return created;
            }

            #endregion
        }

        public void AddAction(FormButton button)
        {
            button.AddToClassList("dialog-frame__action");
            _actions.Add(button);
        }
    }
}
