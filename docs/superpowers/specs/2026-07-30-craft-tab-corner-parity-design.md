# Craft Tab and Corner Grip Visual Parity Design

## Goal

Web UIのインベントリ／クラフト画面について、中央クラフトパネル上部のクラフトタブと右下の三角グリップをuGUI正本と同じ見た目へ収束させる。あわせて、開発時だけ表示される `Ping Action` ボタンを画面から削除する。

完成条件は、同じ基準解像度で撮影した正本とWeb UIの対象cropについて、寸法・相対位置・層構造・輪郭・色が本書の数値条件を満たし、実装に関与していない独立subagentが「目視で区別できる差はない」と判定することである。

## Authoritative References

- uGUI全画面正本: `docs/webui-parity/reference-player-inventory-3270x1844.png`
- uGUIタブ面: `moorestech_client/Assets/Asset/UI/NewUI/Game/craft_tab_select_slice.png`
- uGUI非選択タブ面: `moorestech_client/Assets/Asset/UI/NewUI/Game/craft_tab_base_slice.png`
- uGUIハンマー: `moorestech_client/Assets/Asset/UI/NewUI/Craft/ハンマーのフリーアイコン。.png`
- uGUIクラフトパネル面と右下三角: `moorestech_client/Assets/Asset/UI/NewUI/Game/craft_bg_base_slice.png`
- uGUIタブ構造: `moorestech_client/Assets/Asset/UI/Prefab/Recipe Viwer Tab.prefab`
- uGUIクラフトパネル構造: `moorestech_client/Assets/Asset/UI/Prefab/Inventory/InventoryItems.prefab`
- 現行Web UI:
  - `moorestech_web/webui/src/features/recipe/views/ItemHeader.tsx`
  - `moorestech_web/webui/src/features/recipe/views/ItemHeader.module.css`
  - `moorestech_web/webui/src/shared/ui/GamePanel/index.tsx`
  - `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css`

PNGは形状・色・アルファの測定資料としてのみ使用する。Web UIへPNGをコピー、参照、base64埋め込みしてはならない。

## Confirmed Baseline Differences

### Craft Tab

- 正本の暗色外接は `(1210, 228)–(1375, 297)`、約166×70 screenshot px。
- 正本はクラフトパネル左端 `x=1210` と揃い、タブ下端とパネル上端 `y=300` の間は約3px。
- 正本は前面台形、暗い背面、右側の独立した斜面、細い縁、低彩度の大きなハンマーシルエットから成る。
- 現行Web UIはOS依存のカラー絵文字 `🔨` とCSSの単純な横長台形であり、図像、面積比、傾斜、背面層が一致しない。

### Corner Grip

- 正本の着色された三角明部は約 `(2030, 1364)–(2051, 1385)`、22×22 screenshot px。
- 正本ではパネル右端・下端からそれぞれ約19px内側に置かれる。
- 現行Web UIの着色範囲は約56×42 screenshot pxで、正本比は幅約2.5倍、高さ約1.9倍。
- 現行の3帯グラデーションは正本に存在せず、正本はほぼ単一の青灰面とアンチエイリアスされた斜辺である。
- 現行CSSコメントは約9×9 CSS pxを意図している一方、宣言は `width: 24px; height: 18px` であり、CSS pxとscreenshot pxの換算を取り違えている。

### Ping Action

- `InventoryScreenChrome.tsx` が開発環境だけ `DebugActionButton.tsx` を遅延ロードする。
- `DebugActionButton.tsx` が `debug.echo` を送る `Ping Action` ボタンを描画する。
- 要求はボタンの削除であり、`debug.echo` の通信契約やWebSocketのping/pongは対象外。

## Chosen Approach

uGUIの原Spriteを計測し、Web側はCSSとインラインSVGでベクター再構成する。

CSSだけで形を近似する方式は、複雑なハンマーシルエットとタブの分離した右斜面を安定して表現できず、現在の不一致を再発させるため採用しない。PNGを直接使用する方式は見た目を合わせやすいが、Web UI設計規約の「UI装飾の画像アセット化禁止」に反するため採用しない。

## Component Design

### Craft Tab

`ItemHeader.tsx` の `🔨` を削除し、同じファイル内に装飾専用のインラインSVGを記述する。既存の `views` ディレクトリはすでに10ファイルを超えているため、新しいコンポーネントファイルは追加しない。

SVGは次の独立レイヤーを持つ。

1. `craft_tab_select_slice.png` の暗い背面
2. 同Spriteの前面台形
3. 同Spriteの右側斜面
4. 細い上辺・左辺・斜辺の縁
5. `ハンマーのフリーアイコン。.png` のアルファ輪郭を簡略化したハンマーpath

ハンマーは元画像の低彩度シルエットを維持し、絵文字フォント、カラー、絵文字の光沢、OS依存glyphを使用しない。SVG全体は `aria-hidden="true"` とし、操作要素や読み上げ対象を増やさない。

寸法、位置、面色、縁色、ハンマー色は `moorestech_web/webui/src/app/tokens.css` のクラフトタブ専用固定長・色トークンへ集約する。機能側CSSに新色を直書きしない。固定長を使い、パネル幅比例の `%` は使用しない。

### Corner Grip

`GamePanel` のcraft variantだけが持つ装飾として維持する。現在の `.craft::after` の3帯グラデーションと大型寸法を廃止し、単一の直角三角形へ置換する。

初期CSS値は正本実測をWeb UIのstage倍率へ換算した次の値とする。

- 幅: 9 CSS px
- 高さ: 9 CSS px
- 右端オフセット: 7 CSS px
- 下端オフセット: 7 CSS px

三角面は `craft_bg_base_slice.png` の明部 `rgb(146 148 167)` 系を専用トークン経由で使う。余分な影、3帯グラデーション、二重三角、光彩は付けない。最終値は同条件レンダーの実測で1変数ずつ調整する。

`GamePanel variant="craft"` は中央クラフトパネルのほか `PlacementModeHud` と `ResearchDetailPane` も利用する。正しいグリップ様式を共有variantから供給する既存設計を維持し、利用側へ個別実装を複製しない。中央クラフトパネルを数値一致の正本としつつ、残る2利用画面でもグリップが1個だけ表示され、内容と重ならず、パネル外へはみ出さないことを非破壊ガードとして確認する。

### Ping Action Removal

`InventoryScreenChrome.tsx` から次を削除する。

- `lazy` と `Suspense` のimport
- `DebugActionButton` の条件付き遅延import
- `DebugActionButton` の描画ブロック

利用箇所がなくなる `DebugActionButton.tsx` は削除する。`debug.echo` のAction型、ホスト処理、テスト用通信契約は変更しない。

## Visual Acceptance Criteria

撮影は3270×1844pxで行う。チャレンジHUD追加後の全体レイアウト差を対象装飾の差と誤認しないため、比較cropはクラフトパネル左上を原点として正規化する。対象要素のパネルに対する相対位置は評価対象であり、正規化で位置差を隠してはならない。

### Craft Tab

- 暗色外接寸法: 166×70 screenshot px、各辺の誤差±1px
- タブ左端とパネル左端の差: ±1px
- タブ下端とパネル上端の隙間: 0–3px
- ハンマー外接の目標: 約 `(1254, 248)–(1309, 300)`、各辺±2px
- 前面、暗い背面、右斜面、細い縁がそれぞれ判別でき、正本と同じ重なり順・傾斜方向である
- カラー絵文字、角丸長方形、右端全面の光沢帯、正本にない影を残さない
- 対応する面色は5×5px中央値でRGB各チャンネル±15

### Corner Grip

- 着色外接寸法: 22×22 screenshot px、各辺の誤差±1px
- パネル右端からの隙間: 19px±1px
- パネル下端からの隙間: 19px±1px
- 単一の右下直角三角形で、斜辺方向が正本と一致する
- 面色は5×5px中央値でRGB各チャンネル±15
- 余分な斜め帯、二重三角、影、光彩が存在しない

### Ping Action

- development buildを含め、インベントリ画面に `Ping Action` ボタンが存在しない
- `Ping Action` の可視文字列がWeb UIソースに残らない
- `debug.echo` の通信契約は引き続き型検査と既存テストを通過する

### Independent Visual Verdict

上記数値をすべて満たした後、実装に関与していないfresh subagentへ次を渡す。

- 正本の元画像
- 現行Web UIの元画像
- 両画像の座標グリッド版
- タブの等倍crop
- グリップの等倍crop
- difference画像

subagentが輪郭、層数、面積比、相対位置、色のいずれかに目視可能な差を1件でも挙げた場合は不合格とする。指摘を画素実測で確認し、1変数だけ修正して再撮影する。両要素について「目視で区別できる差はない」と判定されるまで反復する。

アンチエイリアス境界の1px以内かつRGB差15以内の差だけはレンダラー差として許容する。それ以外の輪郭・層・位置・色の差を環境差として除外してはならない。

## Verification

- タブとグリップの構造・計算済み寸法を検証するPlaywrightテスト
- `Ping Action` が存在しないことを検証するPlaywrightテスト
- `PlacementModeHud` と `ResearchDetailPane` における共有craftグリップの存在・個数・内容非重複を確認する非破壊テスト
- 関連Vitest
- Web UI lint
- Web UI production build
- 3270×1844pxのPlaywrightキャプチャ
- 正本と現状のパネル原点正規化crop、blend、difference
- 独立subagentによる反復視覚QA

`.cs`、Unity固有YAML、`.meta` は変更しないため、Unityコンパイルは不要とする。もし実装中に `.cs` 変更が必要になった場合は本仕様の範囲外として停止し、別途承認を得る。

## Non-Goals

- チャレンジHUD導入による画面全体の縦位置変更
- クラフトパネル本体、レシピ選択枠、矢印、CRAFTボタンの再設計
- アイテム名、個数、アイコン、レシピ内容などモックデータ差
- `debug.echo` ActionやWebSocket ping/pongの削除
- uGUIのPNGをWeb配信用アセットとして追加すること
