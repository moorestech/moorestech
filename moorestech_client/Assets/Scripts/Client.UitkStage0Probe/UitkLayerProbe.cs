using Client.Game.InGame.Control;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace Client.UitkStage0Probe
{
    // S1・S2・S3用の前面/背面パネルとボタン。CEFのCanvas(Overlay・sortingOrder 0)を挟んで2枚置く
    // Front/back panels and a button for S1/S2/S3, placed on both sides of the CEF canvas (Overlay, sortingOrder 0)
    public class UitkLayerProbe : MonoBehaviour
    {
        private UIDocument _frontDocument;
        private UIDocument _backDocument;
        private Label _frontStamp;
        private Label _backStamp;
        private Label _statusLabel;
        private bool _lastOverGameObject;
        private bool _lastOverAnyUi;

        public static UitkLayerProbe Instance { get; private set; }
        public Button ProbeButton { get; private set; }
        public VisualElement FrontRoot { get; private set; }
        public int ClickCount { get; private set; }
        public int PointerDownCount { get; private set; }
        public Vector2 LastPointerDownPanelPosition { get; private set; }
        public int LastPointerDownFrame { get; private set; }

        public static UitkLayerProbe Spawn(int frontSortingOrder, int backSortingOrder)
        {
            var gameObject = new GameObject("UitkStage0Probe");
            var probe = gameObject.AddComponent<UitkLayerProbe>();
            probe.Initialize(frontSortingOrder, backSortingOrder);
            Instance = probe;
            return probe;
        }

        public void Initialize(int frontSortingOrder, int backSortingOrder)
        {
            _backDocument = CreateDocument("UitkStage0Probe_Back", backSortingOrder);
            _frontDocument = CreateDocument("UitkStage0Probe_Front", frontSortingOrder);
            BuildBack(_backDocument.rootVisualElement, backSortingOrder);
            BuildFront(_frontDocument.rootVisualElement, frontSortingOrder);
            Debug.Log($"[UitkProbe] spawned front so={frontSortingOrder} back so={backSortingOrder} screen={Screen.width}x{Screen.height}");

            #region Internal

            UIDocument CreateDocument(string documentName, int sortingOrder)
            {
                var child = new GameObject(documentName);
                child.transform.SetParent(transform, false);
                var document = child.AddComponent<UIDocument>();
                document.panelSettings = ProbeAssets.CreatePanelSettings(sortingOrder);
                return document;
            }

            void BuildBack(VisualElement documentRoot, int sortingOrder)
            {
                // 背面: 画面下の帯と左の大きな板。CEFの透明部分からだけ見えるはず
                // Back: a bottom band and a large left plate; should show only through CEF's transparent pixels
                var root = ProbeLayerViews.CreateRoot();
                root.Add(ProbeLayerViews.CreatePlate("back-band", new Color(0f, 0.9f, 0.9f), 0, 880, 1920, 200, $"UITK BACK so={sortingOrder}"));
                root.Add(ProbeLayerViews.CreatePlate("back-left", new Color(0f, 0.9f, 0.9f), 0, 250, 520, 360, $"UITK BACK so={sortingOrder}"));
                _backStamp = ProbeLayerViews.CreateStamp("back-stamp", 20, 620, new Color(0f, 0.35f, 0.35f));
                root.Add(_backStamp);
                documentRoot.Add(root);
            }

            void BuildFront(VisualElement documentRoot, int sortingOrder)
            {
                // 前面: 画面下中央（ホットバー付近）の板と、右の押しボタン
                // Front: a plate over the bottom center (around the hotbar) and a push button on the right
                FrontRoot = ProbeLayerViews.CreateRoot();
                FrontRoot.Add(ProbeLayerViews.CreatePlate("front-hotbar", new Color(1f, 0f, 1f), 660, 960, 600, 110, $"UITK FRONT so={sortingOrder}"));
                _frontStamp = ProbeLayerViews.CreateStamp("front-stamp", 20, 20, new Color(0.45f, 0f, 0.45f));
                FrontRoot.Add(_frontStamp);
                _statusLabel = ProbeLayerViews.CreateStamp("front-status", 20, 70, new Color(0.2f, 0.2f, 0.2f));
                _statusLabel.style.fontSize = 22;
                FrontRoot.Add(_statusLabel);
                ProbeButton = ProbeLayerViews.CreateProbeButton(1560, 420);
                ProbeButton.clicked += OnProbeClicked;
                ProbeButton.RegisterCallback<PointerDownEvent>(OnProbePointerDown, TrickleDown.TrickleDown);
                FrontRoot.Add(ProbeButton);
                documentRoot.Add(FrontRoot);
            }

            #endregion
        }

        public void SetSortingOrders(int frontSortingOrder, int backSortingOrder)
        {
            _frontDocument.panelSettings.sortingOrder = frontSortingOrder;
            _backDocument.panelSettings.sortingOrder = backSortingOrder;
            Debug.Log($"[UitkProbe] sortingOrder front={frontSortingOrder} back={backSortingOrder}");
        }

        public IPanel FrontPanel => ProbeButton.panel;

        public Vector2 ProbeButtonScreenCenter()
        {
            return ProbeCoordinates.PanelToScreen(FrontPanel, ProbeButton.worldBound.center);
        }

        private void OnProbeClicked()
        {
            ClickCount++;
            Debug.Log($"[UitkProbe] button clicked count={ClickCount} frame={Time.frameCount}");
        }

        private void OnProbePointerDown(PointerDownEvent evt)
        {
            PointerDownCount++;
            LastPointerDownPanelPosition = evt.position;
            LastPointerDownFrame = Time.frameCount;
            Debug.Log($"[UitkProbe] pointerdown count={PointerDownCount} panelPos={evt.position} pointerId={evt.pointerId} frame={Time.frameCount}");
        }

        private void Update()
        {
            // 透かし: フレーム番号と実時間。録画・撮影で古いフレームを見分ける
            // Watermark with frame number and real time, so stale frames are identifiable in captures
            var stamp = $"F{Time.frameCount} t={Time.realtimeSinceStartup:0.000}";
            _frontStamp.text = $"FRONT so={_frontDocument.panelSettings.sortingOrder} {stamp}";
            _backStamp.text = $"BACK so={_backDocument.panelSettings.sortingOrder} {stamp}";

            // S3: uGUIのEventSystemとゲームのUI判定がUITKを拾うかを、変化時にログへ出す
            // S3: log, on change, whether the uGUI EventSystem and the game's UI check see UITK
            var eventSystem = EventSystem.current;
            var overGameObject = eventSystem != null && eventSystem.IsPointerOverGameObject();
            var overAnyUi = UiPointerHitTest.IsPointerOverAnyUi();
            var module = eventSystem != null && eventSystem.currentInputModule != null ? eventSystem.currentInputModule.GetType().Name : "none";
            _statusLabel.text = $"IsPointerOverGameObject={overGameObject} UiPointerHitTest={overAnyUi} module={module} clicks={ClickCount} downs={PointerDownCount}";
            if (overGameObject == _lastOverGameObject && overAnyUi == _lastOverAnyUi) return;
            _lastOverGameObject = overGameObject;
            _lastOverAnyUi = overAnyUi;
            Debug.Log($"[UitkProbe] pointer-over changed IsPointerOverGameObject={overGameObject} UiPointerHitTest={overAnyUi} module={module} frame={Time.frameCount}");
        }
    }
}
