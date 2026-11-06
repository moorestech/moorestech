using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// customStyle の角度（"117deg"）を度の数で読む。UssCustomStyleReader に角度の口が無いので画面に置く（2 機能目が出たら Foundation へ昇格を依頼する）
    /// Reads a customStyle angle ("117deg") as degrees; kept in the screen since UssCustomStyleReader has no angle reader (request promotion to Foundation on a second use)
    /// </summary>
    public static class DegreeCustomStyle
    {
        private const string DegreeSuffix = "deg";

        public static bool TryReadDegrees(ICustomStyle style, string propertyName, out float degrees)
        {
            // 角度の値は数として解けないので文字列で受け、末尾の deg を外して読む
            // An angle value does not resolve as a number, so take it as a string and strip the trailing deg
            if (style.TryGetValue(new CustomStyleProperty<string>(propertyName), out var text)
                && text.EndsWith(DegreeSuffix)
                && float.TryParse(text.Substring(0, text.Length - DegreeSuffix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out degrees))
            {
                return true;
            }

            Debug.LogError($"[DegreeCustomStyle] {propertyName} is not a resolved angle in deg: '{text}'");
            degrees = 0f;
            return false;
        }
    }
}
