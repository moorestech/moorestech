using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Client.Playtest.WebUi;
using Client.WebUiHost.Boot;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Client.Tests.Playtest.UiMapping
{
    public class PlaytestWebUiMappingTest
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly List<(FieldInfo Field, object Value)> _saved = new();
        private GameObject _surface;
        private GameObject _sampleObject;
        private WebUiElementLocator _locator;
        private WebSocketHub _hub;

        [SetUp]
        public void Initialize()
        {
            // 実ブラウザを起動せず、実RawImageの座標系とCSS応答を使う
            // Use a real RawImage coordinate system and CSS response without starting a browser
            _surface = new GameObject("Mapping test", typeof(RectTransform), typeof(Canvas));
            _surface.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasRect = _surface.GetComponent<RectTransform>();
            canvasRect.anchorMin = canvasRect.anchorMax = Vector2.zero;
            canvasRect.pivot = Vector2.zero;
            canvasRect.sizeDelta = new Vector2(200f, 200f);
            canvasRect.position = Vector3.zero;
            var imageObject = new GameObject("Surface", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(_surface.transform, false);
            var image = imageObject.GetComponent<RawImage>();
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
            image.rectTransform.pivot = Vector2.zero;
            image.rectTransform.sizeDelta = new Vector2(200f, 200f);
            image.rectTransform.position = Vector3.zero;

            // 既存の非公開キャッシュを差し替え、終了時に元のシーン状態へ戻す
            // Substitute existing private caches and restore the original scene state afterward
            var sampleField = typeof(CefScreenMapper).GetField("_sample", StaticFields);
            _sampleObject = new GameObject("Inactive mapping browser");
            _sampleObject.SetActive(false);
            var sample = _sampleObject.AddComponent(sampleField.FieldType);
            sampleField.FieldType.GetField("_currentWidth", InstanceFields).SetValue(sample, 200);
            sampleField.FieldType.GetField("_currentHeight", InstanceFields).SetValue(sample, 200);
            SaveAndSet(sampleField, sample);
            SaveAndSet(typeof(CefScreenMapper).GetField("_rawImage", StaticFields), image);
            _hub = new WebSocketHub();
            SaveAndSet(typeof(global::Client.WebUiHost.Boot.WebUiHost).GetField("_hub", StaticFields), _hub);
            _locator = new WebUiElementLocator();
            _locator.StartSession();
        }

        [TearDown]
        public void Restore()
        {
            _locator?.StopSession();
            foreach (var saved in _saved) saved.Field.SetValue(null, saved.Value);
            _saved.Clear();
            Object.DestroyImmediate(_sampleObject);
            Object.DestroyImmediate(_surface);
        }

        [UnityTest]
        public IEnumerator FindMapsCssDprAndTopOriginThroughRealSurface()
        {
            var lookup = _locator.Find("target").GetAwaiter();
            Reply(10f, 20f, 30f, 40f, 2f);
            while (!lookup.IsCompleted) yield return null;
            var snapshot = lookup.GetResult();

            // DPR乗算とY反転のどちらを除いても期待矩形から外れる
            // Removing either DPR scaling or Y inversion changes the expected rectangle
            Assert.AreEqual(new Rect(20f, 80f, 60f, 80f), snapshot.ScreenRect);
            Assert.IsTrue(snapshot.Clickable);
        }

        [UnityTest]
        public IEnumerator RepeatedStartPreservesPendingLookupAndRejectsOffscreenCenter()
        {
            var lookup = _locator.Find("target").GetAwaiter();
            _locator.StartSession();
            Reply(210f, 10f, 20f, 20f, 1f);
            LogAssert.Expect(LogType.Warning, "[PlaytestUi] Web element 'target' center is outside the CEF surface");
            while (!lookup.IsCompleted) yield return null;
            var snapshot = lookup.GetResult();
            Assert.IsTrue(snapshot.IsFound);
            Assert.IsFalse(snapshot.Clickable);
            Assert.AreEqual(new Rect(210f, 170f, 20f, 20f), snapshot.ScreenRect);
        }

        private void Reply(float x, float y, float width, float height, float ratio)
        {
            // Findが発行した要求IDへCSS応答を返し、Findの変換経路を最後まで通す
            // Respond to the ID issued by Find so its full mapping path executes
            var query = typeof(WebUiElementLocator).GetField("_domQuery", InstanceFields).GetValue(_locator);
            var channel = typeof(PlaytestDomQuery).GetField("_channel", InstanceFields).GetValue(query);
            var pending = (Dictionary<string, DomQueryResult>)channel.GetType().GetField("_pending", InstanceFields).GetValue(channel);
            Assert.AreEqual(1, pending.Count);
            var requestId = new List<string>(pending.Keys)[0];
            var payload = new JObject
            {
                ["requestId"] = requestId, ["found"] = true, ["visible"] = true,
                ["x"] = x, ["y"] = y, ["width"] = width, ["height"] = height,
                ["devicePixelRatio"] = ratio, ["hitTestPassed"] = true,
                ["text"] = "target", ["matchCount"] = 1, ["stateFlags"] = new JArray(),
            };
            Assert.IsTrue(_hub.ResolveAction("playtest.dom_query_result").ExecuteAsync(payload).GetAwaiter().GetResult().Ok);
        }

        private void SaveAndSet(FieldInfo field, object value)
        {
            _saved.Add((field, field.GetValue(null)));
            field.SetValue(null, value);
        }
    }
}
