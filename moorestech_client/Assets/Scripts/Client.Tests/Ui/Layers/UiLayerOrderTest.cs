using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Client.Ui.Foundation.Layers;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Layers
{
    /// <summary>
    /// 層順とWebの--z-*の一致（L0）
    /// UiLayer order matches Web --z-* (L0)
    /// </summary>
    public class UiLayerOrderTest
    {
        private static readonly string[] WebTokenCssPaths = { "moorestech_web/webui/src/app/tokens.css", "moorestech_web/webui/src/app/skitTokens.css" };
        private const string ViewportStageToken = "--z-viewport-stage";
        private const string ChromeToken = "--z-stage-overlay-panel-chrome";
        private const string AppStylesPath = "moorestech_web/webui/src/app/App.module.css";

        private static readonly Regex CommentPattern = new(@"/\*[\s\S]*?\*/");
        private static readonly Regex ZTokenPattern = new(@"(--z-[\w-]+)\s*:\s*(-?\d+)\s*;");
        private static readonly Regex ViewportRulePattern = new(@"(?m)^\.viewport\s*\{([^}]*)\}");

        // Webの重なり文脈
        // Web stacking contexts
        private enum StackingGroup
        {
            Viewport,
            Stage,
            ChromeNested,
            Portal,
        }

        // 層ごとの (群, トークン名)。層を足したら UiLayer とここを同時に直す
        // (group, token) per layer; when adding a layer, update UiLayer and this table together
        private static readonly Dictionary<UiLayer, (StackingGroup group, string token)> LayerTokens = new()
        {
            { UiLayer.Backdrop, (StackingGroup.Viewport, "--z-viewport-backdrop") },
            { UiLayer.BehindStage, (StackingGroup.Viewport, "--z-viewport-behind-stage") },
            { UiLayer.ViewportStage, (StackingGroup.Viewport, "--z-viewport-stage") },
            { UiLayer.Screen, (StackingGroup.Stage, "--z-stage-screen") },
            { UiLayer.OverlayPanel, (StackingGroup.Stage, "--z-stage-overlay-panel") },
            { UiLayer.Chrome, (StackingGroup.Stage, ChromeToken) },
            { UiLayer.SkitStage, (StackingGroup.ChromeNested, "--z-stage-skit") },
            { UiLayer.PauseMenu, (StackingGroup.Stage, "--z-stage-pause-menu") },
            { UiLayer.Tooltip, (StackingGroup.Stage, "--z-stage-tooltip") },
            { UiLayer.Grab, (StackingGroup.Viewport, "--z-viewport-grab") },
            { UiLayer.WorldPin, (StackingGroup.Portal, "--z-portal-world-pin") },
            { UiLayer.Modal, (StackingGroup.Portal, "--z-portal-modal") },
            { UiLayer.SkitTransition, (StackingGroup.Portal, "--z-portal-skit-transition") },
            { UiLayer.Toast, (StackingGroup.Portal, "--z-portal-toast") },
            { UiLayer.Tutorial, (StackingGroup.Portal, "--z-portal-tutorial") },
            { UiLayer.Reconnect, (StackingGroup.Portal, "--z-portal-reconnect") },
            { UiLayer.FullScreenGate, (StackingGroup.Portal, "--z-portal-full-screen-gate") },
        };

        [Test]
        public void EveryLayerHasTokenMapping()
        {
            foreach (UiLayer layer in Enum.GetValues(typeof(UiLayer)))
            {
                Assert.IsTrue(LayerTokens.ContainsKey(layer), $"No token mapping for {layer}");
            }
        }

        // Portal 群を viewport 群の全層より前に置く根拠。.viewport は position: fixed・z-index 無しなのでルートの z 0 で描かれ、body 直下の Portal（ワールドピン z 10 以上）が常に前に出る（I-fix5 R-7 の Web 撮影で、ピンがブロックのパネルと研究の詳細の前に出るのを確かめた）
        // Basis for ranking the Portal group above every viewport layer: .viewport is position: fixed with no z-index, so it paints at z 0 in the root context and the body-level Portal (world pin z 10 and up) always draws in front (confirmed by the I-fix5 R-7 Web capture: the pin draws over the block panel and the research detail)
        [Test]
        public void ViewportDeclaresNoZIndexSoPortalOutranksIt()
        {
            var text = CommentPattern.Replace(File.ReadAllText(Path.Combine(RepositoryRoot(), AppStylesPath)), "");
            var rule = ViewportRulePattern.Match(text);
            Assert.IsTrue(rule.Success, $".viewport rule is not found in {AppStylesPath}");
            StringAssert.Contains("position: fixed", rule.Groups[1].Value);
            StringAssert.DoesNotContain("z-index", rule.Groups[1].Value, ".viewport got a z-index; re-check whether the Portal group (UiLayer.WorldPin and later) still outranks every viewport layer");
        }

        [Test]
        public void CompositeStackingKeyIncreasesInEnumOrder()
        {
            var zValues = ReadZTokens();
            var layers = (UiLayer[])Enum.GetValues(typeof(UiLayer));

            // 隣り合う層ごとに、後ろの層の合成キーが厳密に大きい
            // For each adjacent pair, the later layer's composite key is strictly greater
            for (var i = 1; i < layers.Length; i++)
            {
                var previous = CompositeKey(layers[i - 1], zValues);
                var current = CompositeKey(layers[i], zValues);
                Assert.Less(previous.CompareTo(current), 0, $"{layers[i - 1]} {previous} is not below {layers[i]} {current}");
            }

            #region Internal

            Dictionary<string, int> ReadZTokens()
            {
                // --z宣言を2ファイルから拾う
                // Pick --z declarations from both files
                var values = new Dictionary<string, int>();
                foreach (var relativePath in WebTokenCssPaths)
                {
                    var text = CommentPattern.Replace(File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath)), "");
                    foreach (Match match in ZTokenPattern.Matches(text))
                    {
                        Assert.IsTrue(values.TryAdd(match.Groups[1].Value, int.Parse(match.Groups[2].Value)), $"Duplicate {match.Groups[1].Value}");
                    }
                }
                return values;
            }

            #endregion
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }

        private static (int portalRank, int viewportZ, int stageZ, int chromeZ) CompositeKey(UiLayer layer, Dictionary<string, int> zValues)
        {
            var (group, token) = LayerTokens[layer];
            var z = ZValue(token, zValues);

            // stage群・skit・portalの入れ子
            // Nesting of stage, skit and portal groups
            return group switch
            {
                StackingGroup.Viewport => (0, z, 0, 0),
                StackingGroup.Stage => (0, ZValue(ViewportStageToken, zValues), z, 0),
                StackingGroup.ChromeNested => (0, ZValue(ViewportStageToken, zValues), ZValue(ChromeToken, zValues), z),
                StackingGroup.Portal => (1, z, 0, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(layer), group, null),
            };
        }

        private static int ZValue(string token, Dictionary<string, int> zValues)
        {
            Assert.IsTrue(zValues.ContainsKey(token), $"{token} is not declared in {string.Join(", ", WebTokenCssPaths)}");
            return zValues[token];
        }
    }
}
