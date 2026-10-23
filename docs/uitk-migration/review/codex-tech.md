技術前提のレビュー完了。**計画は修正して進める判断です。** 指摘は7件。repo・worktreeの編集、Unity起動、実行検証は行っていません。HEADは指定の `e8e611f1c0` と一致しています。

以下、`Scripts/` は `moorestech_client/Assets/Scripts/`、`web/` は `moorestech_web/webui/src/` を指します。

### 1. CEFの無応答検知だけでは、入力抑止が解除されない

- **種別:** 欠落（無いと壊れるもの）
- **対象:** §3.11、§3.3 R4、§4.4
- **根拠:** `Scripts/Client.WebUiHost/Boot/WebSocketHub.cs:153–159` は、送受信ループ終了・接続解除後にだけWeb入力状態をリセットする。`Scripts/Client.Input/InputManager.cs:64–67,181–183` は、その状態で移動・ジャンプを抑止する。一方、§3.11のping途絶時の処置はRawImage非表示だけ。
- **何が起きるか:** **推測される故障:** Web入力欄にフォーカスしたままJSが停止し、WS接続は生き残ると、Dead判定後も `textInputFocused=true` が残る。UITK側でフォーカスを外しても、論理和のWeb側が移動を抑止し続ける。§4.4のプロセス終了試験は接続解除を伴うため、この経路を捕まえない。
- **最小の直し方:**  
  Dead中は入力排他のWeb側を無効にする、と§3.11へ追記する。既存の停止確認を「Web入力欄にフォーカスし、WS接続を残した無応答」に置き換えて確認する。

### 2. `calc()` を一律に計算済みリテラルへ変換できない

- **種別:** 誤り（事実・論理）
- **対象:** §3.5「CSSからの変換規則」
- **根拠:** `web/app/App.tsx:39–47` は解像度から倍率とviewport寸法を実行時に更新する。`web/app/App.module.css:62–65` の `calc()` は、その可変寸法からviewport層の位置を求める。
- **何が起きるか:** 1280×720で計算した位置を固定すると、21:9や16:10で画面端HUDの位置が変わる。これは定数トークンの算術とは異なり、コメントの式とリテラルをL2で検算しても正しく移植できない。
- **最小の直し方:**  
  「定数だけの `calc()` は事前計算。viewport・要素寸法・割合に依存する式は、USSの配置指定または既存の幾何計算へ置換」と書き分ける。確認は既存の§4.6の解像度確認で行う。

### 3. Navigate・Submitを外しても、Tab移動は止まらない

- **種別:** 誤り（事実・論理）
- **対象:** §3.8、§4.8 S4
- **根拠:** Unity公式仕様では、Tab／Shift+TabはEventSystemの入力モジュールに公開されず、アクションの変更では再割当できない。入力欄以外を `focusable=false` にしても、TextFieldは移動先として残る。現Web版は `web/shared/uiState/activeLayer.ts:36–38` でTabを明示的に抑止している。[Unity 6.3 入力FAQ](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-faq-event-and-input-system.html)
- **何が起きるか:** S4の代替案である `NavigationSubmitEvent` の抑止もTabとは別経路なので、現Web版の「Tabでフォーカス移動しない」を満たさない。
- **最小の直し方:**  
  Tabの既定フォーカス移動は別途明示的に抑止する、と§3.8へ記載する。S4から「アクション資産でTabを止められるか」という探索を外し、既存L1で結果を確認する。

### 4. `Q(name)` では重複するtestidを検出できない

- **種別:** 誤り（事実・論理）
- **対象:** §4.4「UITK版の手順」1
- **根拠:** `Q()` は最初の一致要素、またはnullを返すAPIであり、複数件を返さない。[Unity 6.3 `UQueryExtensions.Q`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/UIElements.UQueryExtensions.Q.html)
- **何が起きるか:** 「2件以上なら失敗」という手順を実装できず、動的生成したスロットや複数画面に同名要素があると先勝ちになる。UXMLの静的な名前検査では、実行時の複製まで検出できない。
- **最小の直し方:**  
  `Q(name)` を「`Query(name: …)` で一致要素を列挙し、件数を確認」に置き換える。ロケータも検査も新設する必要はない。

### 5. Painter2DにはSVG path文字列を渡すAPIがない

- **種別:** 誤り（事実・論理）
- **対象:** §4.8 S11、§8.2 D12
- **根拠:** Unity 6000.3.8f1同梱の `UnityEngine.UIElementsModule.xml:15420–15570` と公式APIでは、パス構築は `MoveTo`・`LineTo`・`BezierCurveTo` 等の呼出し。SVGの `d` 文字列を解析する入口はない。[Unity 6.3 `Painter2D`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/UIElements.Painter2D.html)
- **何が起きるか:** S11は描画可否の実測ではなく、SVGパーサーの導入・実装に変わる。約10種のアイコンのために、計画にない保守対象が増える。
- **最小の直し方:**  
  単純な図形はPainter2Dの命令で直接記述し、既存SVGを使いたいアイコンはD12(b)のVectorImageを許す。S11の独立実測は削り、部品の既存目視確認へまとめる。

### 6. Expandの倍率を確かめるS6は、独立した実測にしなくてよい

- **種別:** 過剰（削れるもの）
- **対象:** §4.8 S6、§8.2「agentの既定」
- **根拠:** `Scale With Screen Size` のExpandは、論理パネル寸法が参照解像度を下回らないように拡張する仕様。参照1280×720・追加scale=1なら、倍率は `min(w/1280, h/720)` となる。現Web版も `web/app/App.tsx:39` で同じ計算をしている。[Unity 6.3 Panel Settings](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Runtime-Panel-Settings.html)
- **何が起きるか:** 数式として決まる倍率の探索と、§4.6の複数解像度での配置確認が重複している。ただし、倍率が同じでもstage族とviewport族の配置まで一致するわけではない。
- **最小の直し方:**  
  `Scale With Screen Size / 1280×720 / Expand / scale=1` を既定として確定し、S6を削る。配置の確認は既存の§4.6へ統合する。

### 7. S8・S13は「利用可能か」の探索を縮め、最初のCIテストにまとめられる

- **種別:** 過剰（削れるもの）
- **対象:** §2.1、§4.3、§4.8 S8・S13
- **根拠:** Unity 6000.3.8f1同梱 `com.unity.ui.test-framework` の実コードで、次を確認できる。
  - `Runtime/UITestFixture.cs:98–136`: EditMode用fixtureの生成経路がある。
  - `Runtime/AbstractUITestFixture.cs:229–236`: batchmodeでは `debugMode=false` と明記。
  - `Runtime/PanelSimulator.cs:181–185,278–301`: パネル時計を差し替え、時間加算・スケジューラ・パネル更新を実行する。
- **何が起きるか:** EditMode・batchmode・パネル時計の仕組み自体は既知。これらを別々の可否探索にすると、パッケージ導入と最初のL1テストに重複する。CI固有の設定問題や個別アニメーションの挙動は、引き続き確認が必要。
- **最小の直し方:**  
  S8・S13を「本番テーマの要素でscheduleとtransitionを進めるL1テスト1本を、既存CIへ載せる」に統合する。失敗時は原因を調べ、直ちにL1全体をローカル限定へ変更しない。

**この観点での総評**  
全面移行を否定する技術的障害は、今回の読取調査では確認できませんでした。  
入力停止時の扱いとAPIの誤認を直し、既知仕様の探索を通常の実装・検証へまとめれば、計画を軽くできます。  
S2の録画、S9のIME、S14の配布版HttpListenerは、実環境依存が残るため実測を維持してください。

=====================
セッションの終了可否 : OK
=====================
