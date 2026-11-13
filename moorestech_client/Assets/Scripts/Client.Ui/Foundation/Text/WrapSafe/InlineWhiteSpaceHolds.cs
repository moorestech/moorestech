using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text.WrapSafe
{
    /// <summary>
    /// 1 つの文の要素の inline の white-space を、理由ごとの「折り返しを止める」の重ね合わせで持つ
    /// Owns a text element's inline white-space as the union of per-reason "stop wrapping" holds
    /// </summary>
    /// <remarks>
    /// 行末の約物の詰めと狭い幅の回避が同じ inline の値を書くので、止める前の inline の値と解決済みの値（USS が望む折り返し）を最初に止めたときに一度だけ写し、全部の理由が外れたら戻す。
    /// The line-end trim and the narrow-width guard write the same inline value, so the inline value and the resolved value (the wrap USS wants) are captured once on the first hold and restored when every reason is released.
    /// </remarks>
    internal class InlineWhiteSpaceHolds
    {
        private readonly VisualElement _element;
        private WhiteSpaceHoldReason _held;
        private StyleEnum<WhiteSpace> _inlineBeforeHold;
        private WhiteSpace _resolvedBeforeHold;

        public InlineWhiteSpaceHolds(VisualElement element)
        {
            _element = element;
        }

        // 解決済みの値、止め中は止める前の USS 値
        // The resolved value when nothing holds, otherwise the value USS wanted before the first hold
        public WhiteSpace Wanted => _held == WhiteSpaceHoldReason.None ? _element.resolvedStyle.whiteSpace : _resolvedBeforeHold;

        public bool IsHeld(WhiteSpaceHoldReason reason)
        {
            return (_held & reason) != 0;
        }

        // 指定の理由を外したら折り返すか（他の理由が止めていれば折り返さない）
        // Whether the text would wrap with this reason released (another holding reason keeps it unwrapped)
        public bool WrapsWithout(WhiteSpaceHoldReason reason)
        {
            if ((_held & ~reason) != WhiteSpaceHoldReason.None) return false;
            var wanted = Wanted;
            return wanted == WhiteSpace.Normal || wanted == WhiteSpace.PreWrap;
        }

        public void Hold(WhiteSpaceHoldReason reason)
        {
            if (_held == WhiteSpaceHoldReason.None)
            {
                _inlineBeforeHold = _element.style.whiteSpace;
                _resolvedBeforeHold = _element.resolvedStyle.whiteSpace;
                _element.style.whiteSpace = WhiteSpace.NoWrap;
            }
            _held |= reason;
        }

        public void Release(WhiteSpaceHoldReason reason)
        {
            if (!IsHeld(reason)) return;
            _held &= ~reason;
            // silent-ok: 他の理由が残る間は止めたまま / その理由が外れたときにここで戻す
            if (_held != WhiteSpaceHoldReason.None) return;
            _element.style.whiteSpace = _inlineBeforeHold;
        }
    }
}
