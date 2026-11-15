using System.Text.RegularExpressions;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Theme.StyleResolution;
using NUnit.Framework;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Theme.StyleResolution
{
    /// <summary>
    /// トークン待ちの門が解決を 1 回通知し、配置後も未解決なら開発者へ出すことを確かめる（L1）
    /// Verifies the token gate announces resolution once and tells developers when still unresolved after layout (L1)
    /// </summary>
    public class UssStyleResolutionGateTest : UiTestHost
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Theme/UssCustomStyleProbe.uss";

        [Test]
        public void パネルに載せてUSSが解決すると1回だけ通知し解決済みになる()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(sheet, ProbePath);
            rootVisualElement.styleSheets.Add(sheet);
            var host = new VisualElement();
            host.AddToClassList("uss-custom-style-probe");
            var gate = new UssStyleResolutionGate(host, "ProbeHost");
            var resolvedCount = 0;
            using var subscription = gate.OnResolved.Subscribe(_ => resolvedCount++);
            Assert.IsFalse(gate.IsResolved);

            rootVisualElement.Add(host);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(1, resolvedCount);
            Assert.IsTrue(gate.IsResolved);
        }

        [Test]
        public void USSの無いまま配置まで進むと開発者にエラーを出す()
        {
            var host = SizedBareHost();
            var gate = new UssStyleResolutionGate(host, "ProbeHost");
            LogAssert.Expect(LogType.Error, new Regex(@"\[ProbeHost\] custom style not resolved"));
            rootVisualElement.Add(host);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsFalse(gate.IsResolved);
        }

        [Test]
        public void 配置前の預かりは黙り配置後の預かりはエラーを出す()
        {
            // 配置前の預かりは一時的な正常系なので何も出さない
            // Holding before layout is the transient normal case, so nothing is logged
            var host = SizedBareHost();
            var gate = new UssStyleResolutionGate(host, "ProbeHost");
            gate.ReportHeldWhileUnresolved("probe request");
            LogAssert.NoUnexpectedReceived();

            // 配置後も未解決なら、配置の時点と預かりの時点の 2 件を出す
            // When still unresolved after layout, log once at layout and once at the hold
            LogAssert.Expect(LogType.Error, new Regex(@"\[ProbeHost\] custom style not resolved"));
            rootVisualElement.Add(host);
            simulate.FrameUpdate();
            LogAssert.Expect(LogType.Error, new Regex(@"^\[ProbeHost\] probe request is held because the custom style never resolved"));
            gate.ReportHeldWhileUnresolved("probe request");
        }

        // 規則の当たらない要素（tss の @import 漏れと同じ状態）。寸法を持たせて配置の通知を確実に起こす
        // An element no rule matches (same as a missing tss @import); sized so the layout notification surely fires
        private static VisualElement SizedBareHost()
        {
            var host = new VisualElement();
            host.style.width = 10f;
            host.style.height = 10f;
            return host;
        }
    }
}
