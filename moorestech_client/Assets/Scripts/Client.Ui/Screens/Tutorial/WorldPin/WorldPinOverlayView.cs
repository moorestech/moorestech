using System.Collections.Generic;
using System.Linq;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.Tutorial.Pulse;
using Client.Ui.State.Tutorial.Logic;
using Client.Ui.State.Tutorial.WorldPin;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.WorldPin
{
    /// <summary>
    /// ワールドピンを全面に重ねる画面。購読はせず、Presenter から Render を受ける（Web WorldPinOverlay）
    /// Full-screen layer of world pins; subscribes to nothing and receives Render from the Presenter (Web WorldPinOverlay)
    /// </summary>
    public sealed class WorldPinOverlayView : VisualElement
    {
        private readonly Dictionary<string, WorldPinMarkerView> _markers = new();
        private readonly Dictionary<string, WorldPinEdgeArrowView> _arrows = new();
        private readonly TutorialPulseBinding _pulse;
        private WorldPinViewModel _current = WorldPinViewModel.Empty;
        private float _edgeMargin;
        private float _arrowSize;
        private Color _face;
        private Color _stroke;
        private Color _arrowFill;
        private Color _arrowShadow;
        private bool _knobsResolved;

        public WorldPinOverlayView()
        {
            name = "world-pin-overlay";
            AddToClassList("world-pin-overlay");
            pickingMode = PickingMode.Ignore;
            _pulse = new TutorialPulseBinding(this);
            RegisterCallback<CustomStyleResolvedEvent>(evt => ReadKnobs(evt.customStyle));
            // 層の寸法が変わったら端クランプを置き直す（Web useViewportSize）
            // Re-clamp the edge arrows when the layer size changes (Web useViewportSize)
            RegisterCallback<GeometryChangedEvent>(_ => Render(_current));
            style.display = DisplayStyle.None;
        }

        public void Render(WorldPinViewModel model)
        {
            _current = model;
            // 画面内外が切り替わったピンは作り直すため、先に今の形と合わないものを外す
            // Pins that switched on/off screen are rebuilt, so first drop views that no longer match
            RemoveStale();
            foreach (var cue in model.Pins)
            {
                if (cue is WorldPinCue.OnScreen onScreen) ShowMarker(onScreen);
                else ShowArrow((WorldPinCue.OffScreen)cue);
            }
            style.display = model.Pins.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            #region Internal

            void ShowMarker(WorldPinCue.OnScreen cue)
            {
                if (!_markers.TryGetValue(cue.PinId, out var marker))
                {
                    // 文を当ててから木に載せる（文の無い LocalizedLabel は LogError。Overlay のラベルと同形）
                    // Attach only after the text is set (a text-less LocalizedLabel logs an error; same as the overlay label)
                    marker = new WorldPinMarkerView();
                    marker.Show(cue);
                    _markers.Add(cue.PinId, marker);
                    Add(marker);
                }
                else marker.Show(cue);
                if (_knobsResolved) marker.SetColors(_face, _stroke);
            }

            void ShowArrow(WorldPinCue.OffScreen cue)
            {
                if (!_arrows.TryGetValue(cue.PinId, out var arrow))
                {
                    arrow = new WorldPinEdgeArrowView(_pulse);
                    _arrows.Add(cue.PinId, arrow);
                    Add(arrow);
                }
                // silent-ok: 寸法と余白の解決前・層のレイアウト前は隠し、CustomStyleResolvedEvent と GeometryChangedEvent で置き直す / L1 WorldPinOverlayTest が位置を見る
                if (!_knobsResolved || !WorldPinEdgePlacement.TryPlace(layout.width, layout.height, _edgeMargin, cue.DirectionX, cue.DirectionY, out var spot))
                {
                    arrow.HideArrow();
                    return;
                }
                arrow.SetColors(_arrowFill, _face, _arrowShadow);
                arrow.Show(cue.PinId, spot, _arrowSize);
            }

            void RemoveStale()
            {
                // 消えたピンと、画面内外が切り替わったピンの View を外す
                // Remove views of vanished pins and of pins that switched on/off screen
                var onScreen = new HashSet<string>(model.Pins.Where(cue => cue is WorldPinCue.OnScreen).Select(cue => cue.PinId));
                var offScreen = new HashSet<string>(model.Pins.Where(cue => cue is WorldPinCue.OffScreen).Select(cue => cue.PinId));
                foreach (var pinId in _markers.Keys.Where(pinId => !onScreen.Contains(pinId)).ToList())
                {
                    _markers[pinId].RemoveFromHierarchy();
                    _markers.Remove(pinId);
                }
                foreach (var pinId in _arrows.Keys.Where(pinId => !offScreen.Contains(pinId)).ToList())
                {
                    _arrows[pinId].Discard();
                    _arrows.Remove(pinId);
                }
            }

            #endregion
        }

        private void ReadKnobs(ICustomStyle customStyle)
        {
            // 寸法と色はトークンを画面ルートで受け直した変数から読み、読めたら描き直す（読めなければ Reader が LogError）
            // Read sizes and colors from the screen root's re-declared tokens and redraw (the reader logs an error when unreadable)
            var hasMargin = UssCustomStyleReader.TryReadPixels(customStyle, "--world-pin-view-edge-margin", out _edgeMargin);
            var hasSize = UssCustomStyleReader.TryReadPixels(customStyle, "--world-pin-view-arrow-size", out _arrowSize);
            var hasFace = UssCustomStyleReader.TryReadColor(customStyle, "--world-pin-view-face", out _face);
            var hasStroke = UssCustomStyleReader.TryReadColor(customStyle, "--world-pin-view-stroke", out _stroke);
            var hasFill = UssCustomStyleReader.TryReadColor(customStyle, "--world-pin-view-arrow-fill", out _arrowFill);
            var hasShadow = UssCustomStyleReader.TryReadColor(customStyle, "--world-pin-view-arrow-shadow", out _arrowShadow);
            _knobsResolved = hasMargin && hasSize && hasFace && hasStroke && hasFill && hasShadow;
            Render(_current);
        }
    }
}
