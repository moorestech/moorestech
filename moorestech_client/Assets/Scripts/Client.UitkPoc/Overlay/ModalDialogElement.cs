using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Overlay
{
    // 背景を暗くして中央に出すモーダル。Escか背景クリックで閉じる
    // Modal over a dimmed backdrop; closes on Esc or a backdrop click
    public class ModalDialogElement : VisualElement
    {
        private readonly VisualElement _dialog = new();

        public ModalDialogElement()
        {
            AddToClassList("ov-backdrop");
            AddToClassList("ov-backdrop--hidden");
            _dialog.AddToClassList("ov-dialog");
            _dialog.Add(new Label("<b>ワールドを削除しますか？</b>"));
            _dialog.Add(new Label("この操作は取り消せません。（Esc・背景クリックで閉じる）"));
            var name = new TextField("確認のため名前を入力") { value = "" };
            var buttons = new VisualElement();
            buttons.AddToClassList("ov-dialog__buttons");
            buttons.Add(new Button(Close) { text = "キャンセル" });
            buttons.Add(new Button(Close) { text = "削除する" });
            _dialog.Add(name);
            _dialog.Add(buttons);
            Add(_dialog);
            focusable = true;

            // 背景そのものを押したときだけ閉じる（ダイアログ内のクリックは閉じない）
            // Close only when the backdrop itself is pressed, not the dialog
            RegisterCallback<PointerDownEvent>(evt => { if (evt.target == this) Close(); });
            RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) Close(); }, TrickleDown.TrickleDown);
        }

        public void Open()
        {
            BringToFront();
            RemoveFromClassList("ov-backdrop--hidden");
            schedule.Execute(() => _dialog.Q<TextField>().Focus()).ExecuteLater(50);
        }

        private void Close()
        {
            AddToClassList("ov-backdrop--hidden");
            Blur();
        }
    }
}
