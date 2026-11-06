using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.TestDoubles
{
    /// <summary>
    /// UiText のキーと引数（名前と値の並び）を 1 行で検査する
    /// Asserts a UiText's key and params (ordered name/value pairs) in one line
    /// </summary>
    public static class UiTextAssert
    {
        public static void AreEqual(LocalizationKey expectedKey, string[] expectedNameValuePairs, UiText actual)
        {
            AreEqual(expectedKey.Key, expectedNameValuePairs, actual);
        }

        public static void AreEqual(LocalizationKey expectedKey, UiText actual)
        {
            AreEqual(expectedKey.Key, new string[0], actual);
        }

        public static void AreEqual(string expectedKey, string[] expectedNameValuePairs, UiText actual)
        {
            // キー → 引数の数 → 各引数の名前と値の順に比べる
            // Compare the key, then the param count, then each param's name and value
            Assert.AreEqual(expectedKey, actual.Key.Key, "key");
            Assert.AreEqual(expectedNameValuePairs.Length / 2, actual.Params.Count, $"param count of {expectedKey}");
            for (var i = 0; i < actual.Params.Count; i++)
            {
                Assert.AreEqual(expectedNameValuePairs[i * 2], actual.Params[i].Name, $"param name #{i} of {expectedKey}");
                Assert.AreEqual(expectedNameValuePairs[i * 2 + 1], actual.Params[i].Value, $"param value #{i} of {expectedKey}");
            }
        }
    }
}
