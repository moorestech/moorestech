using System.Diagnostics;
using Client.Localization;
using Client.Starter.Initialization.Progress;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Client.Tests.Starter
{
    /// <summary>
    ///     進捗1行が実辞書を通り、引数順どおりに表示テキストへ届くことを検証
    ///     Verifies one progress line travels the real dictionary and reaches the displayed text in argument order
    /// </summary>
    public class LoadingProgressLogTest
    {
        private GameObject _loadingLogObject;

        [SetUp]
        public void SetUp()
        {
            // 文言解決は実辞書を通す
            // Resolve text through the real dictionary
            Localize.Initialize();

            _loadingLogObject = new GameObject("LoadingLog");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_loadingLogObject);
        }

        [Test]
        public void 経過時間つきの行は先頭の値と経過時間が引数順で入る()
        {
            var loadingLog = _loadingLogObject.AddComponent<TextMeshProUGUI>();
            loadingLog.text = string.Empty;

            // 止まったままの計時で経過時間を固定する
            // A stopwatch left unstarted pins the elapsed value
            var stopwatch = new Stopwatch();
            var progressLog = new LoadingProgressLog(loadingLog, stopwatch);

            progressLog.AppendElapsed(LocalizationKeys.Ui.Loading.ServerConnected);
            progressLog.AppendElapsed(LocalizationKeys.Ui.Loading.TerrainReady, "3");

            var lines = loadingLog.text.Split('\n');
            Assert.AreEqual(3, lines.Length);
            Assert.AreEqual(string.Empty, lines[0]);

            var elapsed = stopwatch.Elapsed.ToString();
            Assert.AreEqual(Localize.GetFormatted(LocalizationKeys.Ui.Loading.ServerConnected, new[] { elapsed }), lines[1]);

            // {p0}にチャンク数、{p1}に経過時間が入る順序を固定する
            // Pins that {p0} takes the chunk count and {p1} the elapsed time
            Assert.AreEqual(Localize.GetFormatted(LocalizationKeys.Ui.Loading.TerrainReady, new[] { "3", elapsed }), lines[2]);
            StringAssert.Contains("3", lines[2]);
        }
    }
}
