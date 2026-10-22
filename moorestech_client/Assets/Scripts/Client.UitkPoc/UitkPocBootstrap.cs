using System.Collections.Generic;
using Client.UitkPoc.Effects;
using Client.UitkPoc.Motion;
using Client.UitkPoc.Overlay;
using Client.UitkPoc.PowerChart;
using Client.UitkPoc.Preview3D;
using Client.UitkPoc.RecipeGraph;
using Client.UitkPoc.Shell;
using Client.UitkPoc.TextAndInput;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Client.UitkPoc
{
    // UitkPocシーンの入口。全デモをタブに並べてUIDocumentへ載せる
    // Entry of the UitkPoc scene; lists every demo as a tab on the UIDocument
    public class UitkPocBootstrap : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Font japaneseFont;
        [SerializeField] private string modDirectory;

        private UitkPocShell _shell;

        private void Start()
        {
            // FontDefinition.FromFont(Font)はビットマップ描画になり拡大で崩れるため、SDFの動的フォントアセットを作る
            // FromFont(Font) renders bitmap glyphs that break when scaled, so build a dynamic SDF font asset
            var sdfFont = FontAsset.CreateFontAsset(japaneseFont, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            var context = new UitkPocContext(sdfFont, japaneseFont, modDirectory);
            ApplyItemIconSprites(context);
            var demos = new List<IUitkPocDemo>
            {
                new RecipeGraphDemo(context),
                new PowerChartDemo(context),
                new Preview3DDemo(context),
                new EffectsDemo(context),
                new MotionDemo(context),
                new TextAndInputDemo(context),
                new OverlayDemo(context),
            };

            // シェルのUSSと日本語フォントをルートへ与える
            // Give the root the shell stylesheet and the Japanese font
            var root = uiDocument.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UitkPoc/PocShell"));
            root.styleSheets.Add(Resources.Load<StyleSheet>("UitkPoc/PocControls"));
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(sdfFont);
            _shell = new UitkPocShell(demos);
            root.Add(_shell.Root);
            _shell.Show(demos[0]);
        }

        private void ApplyItemIconSprites(UitkPocContext context)
        {
            // <sprite name="アイテム名">はパネルのテキスト設定の既定SpriteAssetから引かれるため、複製したPanelSettingsへ差し込む
            // <sprite name="item"> resolves from the panel's default SpriteAsset, so inject it into a cloned PanelSettings
            var textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
            textSettings.defaultSpriteAsset = ItemIconSpriteAssetLoader.LoadAllItems(context);
            var panelSettings = Instantiate(uiDocument.panelSettings);
            panelSettings.textSettings = textSettings;
            uiDocument.panelSettings = panelSettings;
        }

        private void Update()
        {
            _shell.Tick(Time.unscaledDeltaTime);
        }

        // uloopの動的コードからタブを切り替え、計測値を読むための入口
        // Entry for uloop dynamic code to switch tabs and read measurements
        public void ShowDemo(string title)
        {
            _shell.ShowByTitle(title);
        }

        public string DescribeMeter()
        {
            var meter = _shell.Meter;
            return $"{_shell.ActiveDemo.Title}: frame avg {meter.LastAverageMs:0.00}ms worst {meter.LastWorstMs:0.00}ms / UITK avg {meter.LastUitkAverageMs:0.00}ms max {meter.LastUitkWorstMs:0.00}ms";
        }
    }
}
