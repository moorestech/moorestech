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
    /// UiLayer の並びが Web の tokens.css の --z-* が作る重なり順と一致することを確かめる（L0）
    /// Verifies the UiLayer order matches the stacking order built from --z-* in the Web tokens.css (L0)
    /// </summary>
    public class UiLayerOrderTest
    {
        private const string WebTokenCssPath = "moorestech_web/webui/src/app/tokens.css";
        private const string ViewportStageToken = "--z-viewport-stage";

        private static readonly Regex CommentPattern = new(@"/\*[\s\S]*?\*/");
        private static readonly Regex ZTokenPattern = new(@"(--z-[\w-]+)\s*:\s*(-?\d+)\s*;");

        // Web の重なり文脈。viewport 直下・viewport の stage 枠内・viewport 全体より前の portal
        // Web stacking contexts: directly in viewport, inside viewport's stage slot, and portal above the whole viewport
        private enum StackingGroup
        {
            Viewport,
            Stage,
            Portal,
        }

        // 層ごとの (群, トークン名)。層を足したら UiLayer とここを同時に直す
        // (group, token) per layer; when adding a layer, update UiLayer and this table together
        private static readonly Dictionary<UiLayer, (StackingGroup group, string token)> LayerTokens = new()
        {
            { UiLayer.Backdrop, (StackingGroup.Viewport, "--z-viewport-backdrop") },
            { UiLayer.BehindStage, (StackingGroup.Viewport, "--z-viewport-behind-stage") },
            { UiLayer.Screen, (StackingGroup.Stage, "--z-stage-screen") },
            { UiLayer.Hud, (StackingGroup.Stage, "--z-stage-overlay-panel") },
            { UiLayer.PauseMenu, (StackingGroup.Stage, "--z-stage-pause-menu") },
            { UiLayer.Tooltip, (StackingGroup.Stage, "--z-stage-tooltip") },
            { UiLayer.Grab, (StackingGroup.Viewport, "--z-viewport-grab") },
            { UiLayer.WorldPin, (StackingGroup.Portal, "--z-portal-world-pin") },
            { UiLayer.Modal, (StackingGroup.Portal, "--z-portal-modal") },
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
        }

        private static (int portalRank, int viewportZ, int innerZ) CompositeKey(UiLayer layer, Dictionary<string, int> zValues)
        {
            var (group, token) = LayerTokens[layer];
            var z = ZValue(token, zValues);

            // stage 群は viewport の stage 枠の位置に入れ子、portal 群は viewport 全体より前
            // The stage group nests at the viewport's stage slot; the portal group sits above the whole viewport
            return group switch
            {
                StackingGroup.Viewport => (0, z, 0),
                StackingGroup.Stage => (0, ZValue(ViewportStageToken, zValues), z),
                StackingGroup.Portal => (1, z, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(layer), group, null),
            };
        }

        private static int ZValue(string token, Dictionary<string, int> zValues)
        {
            Assert.IsTrue(zValues.ContainsKey(token), $"{token} is not declared in {WebTokenCssPath}");
            return zValues[token];
        }

        private static Dictionary<string, int> ReadZTokens()
        {
            // コメントを除いてから「--z-name: 整数;」の宣言だけを拾う
            // Strip comments, then pick only "--z-name: integer;" declarations
            var path = Path.Combine(RepositoryRoot(), WebTokenCssPath);
            var text = CommentPattern.Replace(File.ReadAllText(path), "");
            var values = new Dictionary<string, int>();
            foreach (Match match in ZTokenPattern.Matches(text))
            {
                Assert.IsTrue(values.TryAdd(match.Groups[1].Value, int.Parse(match.Groups[2].Value)), $"Duplicate {match.Groups[1].Value}");
            }
            return values;
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
