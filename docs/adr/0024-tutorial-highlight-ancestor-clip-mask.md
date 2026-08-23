# チュートリアルハイライトを祖先のoverflowクリップに合わせてマスクする

`TutorialOverlay` は `<Portal>` で body 直下に置かれた `position: fixed` 層に描かれ、ハイライトはアンカーのビューポート座標で配置される（`app/App.tsx` の Portal、`features/tutorial/style.module.css` の `.overlay`）。アンカー側は別の親を持つため、祖先の `overflow: hidden` によるクリップがハイライトへ一切継承されない。可視判定 `resolveTutorialAnchor` も `innerWidth` / `innerHeight`、すなわち**ウィンドウ**の外に出たかどうかしか見ていない。

結果、研究ツリーのノードをパンで表示領域の外へ追い出すと、ノード本体は `TreeView.module.css` の `.viewport { overflow: hidden }` で消えるのに、ハイライト枠だけがパネルの外に取り残されて描画され続ける。TreeView は仮想化していないためアンカー要素は DOM に生存し、`getBoundingClientRect()` は祖先クリップを反映しない完全な矩形を返し続け、`zero-area` 判定にも掛からない。

同型の穴はチャレンジツリー（同じ TreeView）、レシピのアイテム一覧（`ScrollArea.Autosize`）、ビルドメニュー（`ScrollArea`）、D&Dガイド矢印にもある。

計測: 部分クリップでも矩形は一切縮まない。ノードのパネル内可視率が 0.08 でもハイライトはフルサイズで描かれ、可視率と status は無相関で最後まで `ready` のままだった。

## 決定

`resolveTutorialAnchor` がアンカーの祖先を辿って実クリップ矩形の交差を求め、戻り値へ `clip` として載せる。`TutorialOverlay.renderOutline` はハイライトのボックス座標系へ変換して `clip-path: inset()` を当てる。

walk は feature の知識を持たず、`data-tutorial-anchor` が付いた任意の要素に効く。ブラウザ自身の `IntersectionObserver.intersectionRect` をオラクルにして15形状（TreeView風パン・ネイティブスクロール・二重ネスト・sticky・fixed脱出・fixed捕捉・absolute脱出・contain:paint・clip-path祖先・border/paddingインセット・三重ネスト・scaleズーム・ウィンドウ端・flex min-width:0）で突き合わせ、全一致を確認済み。コストは1アンカーあたり約6µsで、rAF ごとに全アンカー回しても問題ない。

出所: agent前提（AGENTS.md「汎用基盤にドメイン語彙を持ち込まない」の適用。walk は上位の業務概念を知らず DOM 構造だけで判断する）

戻り値に載せるのは「クリップ矩形」であって「アンカー矩形との交差」ではない。ハイライトは `paddingPx` ぶんアンカーより外へ広がるため、交差済み矩形だけ渡すとリングが常にアンカー端で切られ、余裕があるときでもリングが消える。

出所: agent前提（paddingPx の実装から導出）

交差が空、すなわちアンカーが完全にクリップされている場合、`renderOutline` は `null` を返して要素自体を作らない。`clip-path` で潰した要素は Playwright が `isVisible() === true`・`boundingBox()` が元寸法のまま返すため（実測）、DOM と実態が食い違いテストが嘘をつく。

出所: ユーザー裁定 2026-08-20「描画しない（nullを返す）」

## Considered Options

- **実行時に `IntersectionObserver.intersectionRect` を使う**（却下）: ブラウザがクリップ規則を正しく計算するので walk が不要になるが、IO は非同期配信でパン中に rAF 更新から遅れ、マスク端がノード端を追い越してチラつく。ハイライトは毎フレーム追従が要るため致命的。
- **ハイライトをクリップ元コンテナへ Portal する**（却下）: CSS が本来の規則でマスクするので数式ゼロ、角丸も自動で正しい。ただし stacking context がパネル内に入り重なり順の契約が変わる。クリップ元を持たないアンカー（クロスヘア等）と2系統になる分岐も増える。walk がブラウザと全形状で一致した以上、walk は実質「ブラウザの規則を関数にしたもの」であり、契約を変えてまで採る理由がない。
- **`dragGuide` も今回マスクする**（却下）: 矢印は from→to を translate アニメーションで移動するため、どの時点のどのクリップ矩形を当てるかが自明でない（from側固定・to側固定・両者の和・アニメーション追従の4案が立つ）。設計判断が1つ増え実装とテストが膨らむ。
- **ack セマンティクスも今回直す**（却下）: 完全クリップ時に `hidden` / 新reason `clipped` を返す案。入れると「1pxだけ見えている状態を表示成功とみなすか」の閾値が即座に論点化するが、閾値は未裁定。
- **walk をプロダクションへ露出して e2e から直接呼ぶ**（却下）: 検証としては最強だが AGENTS.md「デバッグ/テスト専用publicをプロダクションに残さない」に正面から抵触する。
- **合成15形状のオラクルテストをリポジトリに残す**（却下）: 下記のとおり実画面が脱出則を踏まないため、moorestech に存在しない形を守ることになる。開発中の道具としては使うが成果物には含めない。
- **実ゲーム（unityプレイ録画テスト）まで通して見た目を確認する**（却下）: webui のレンダリングは mock-host と同一で、今回のバグは純粋に webui の幾何問題のため追加情報が少ない。Unity Editor 起動・Library コピー・master ピンのコストに見合わない。

出所: ユーザー裁定 2026-08-20（スコープ・完全クリップ時の描画・テスト方針・見た目確認範囲の4件）

## テスト

実UI経路 × ブラウザオラクルの1本（`e2e/tests/system/`）。実アプリの研究画面でハイライトを出し、実マウスのドラッグパンで「内側 → 部分 → 完全に外」へ通し、各段階でハイライトの可視領域をアンカーの `IntersectionObserver.intersectionRect` と突き合わせる。期待値を手で書かないためレイアウト変更で腐らず、`clip` を `clip-path` へ渡し忘れる結線ミスも同時に守る。

実画面は `.viewport`（`position: fixed; overflow: hidden`）→ `.stage`（`position: relative; transform: scale()`）→ `.treeContainer`（`overflow: hidden`）→ TreeView `.viewport`（`overflow: hidden`）→ canvas（transform パン）という四重ネストを通るため、合成形状のうち意味のある部分は実質カバーされる。

出所: ユーザー裁定 2026-08-20「実UI経路 × ブラウザオラクル 1本」

## Consequences

- **脱出則はデッドロジックではなく、実UIで毎フレーム評価されている。** `position: fixed` なアンカーは実在する — `commonHud/style.module.css` の `.crosshair`（`CommonHud.tsx` が `TutorialAnchorIds.gameCrosshair` を付与）と `trainHud/style.module.css` の `.hud`（`TrainRidingHud.tsx` が `TutorialAnchorIds.trainHudStatus` を付与）。どちらも `App.tsx` で `.stage` 直下に置かれ、`.stage`（`App.module.css` の `transform: scale(var(--ui-scale,1))`）が fixed の包含ブロックになるため、escape 分岐は `.stage` で即座に捕捉される。そのため今のところマスク結果は変わらないが、評価自体は起きている。将来スクロールコンテナ内へ `position: fixed` のモーダルを置いた場合に結果が変わり、誤ると**ハイライトが丸ごと消える**（現行バグより悪い壊れ方）。`ancestorClip.ts` にこの旨を明記する。
- **アンカーがクリップ端に密着している場合、`paddingPx` のリングだけが切られて枠が「コ」の字に欠ける。** ノードが100%見えていても起きる。レシピのアイテム一覧（`.mantine-ScrollArea-viewport` は `padding-left` のみで上パディング無し）とビルドメニュー（`.scroll` はパディング無し）の最上段で実際に発生する。今回は許容する。
  - **追記2 2026-08-22（解消）**: レシピのアイテム一覧については逃げを実装して解消した。クリップ境界とグリッドの距離を実測すると 上0px / 左3.13px しかなく、必要量（`paddingPx` 8px + `--tutorial-highlight-glow` 4px = 12px）に届いていなかった。**マスク矩形の計算は正しく、足りていなかったのは padding のほう**。viewport へ `--tutorial-anchor-clip-inset` の padding を入れ、同量の負マージンを ScrollArea へ入れて相殺することで、クリップ境界だけを外へ広げた（グリッドの絶対位置・内容box寸法・7段/8段の溢れ閾値・ノブ位置はいずれも不変）。結果 `clip-path` は四辺とも `inset(-4px)`＝無削りになった。
  - **追記3 2026-08-22（全スクローラへ展開・逃げ量の値源）**: レビュー裁定により、逃げをレシピ単一リスト（`.recipeListScroll`）とビルドメニュー（`.scroll`）へも同形で適用した。さらに独立レビュー裁定（2026-08-23）で4つ目のクリップ容器 `shared/treeView/TreeView.module.css` の `.viewport` へも展開し、包む側（`ResearchTreePanel` の `.treeContainer`）の `overflow: hidden` を外した — 外側で切ると内側で広げたクリップ境界が戻ってしまうため。あわせて `TreeView` のpan/zoomは基準を内容boxへ揃えた（padding分のずれを消す `toContentBox`）。アンカーを持つクリップ容器は4つとも対応済みで、「規則はあるが実装が追随していない」状態を残さない。**逃げ量の正本はCSSの `--tutorial-anchor-padding` とする**（同裁定で書き戻し機構は撤廃）。逃げはスクロール領域の高さを変え段数の溢れ閾値を動かすため、実行中に変わる値であってはならない。マスタの `paddingPx` はこのトークンを超えない前提で使う。e2e もリテラルから「逃げ ≧ `--tutorial-anchor-padding` + `--tutorial-highlight-glow`」の関係式検査へ変えた。
  - **追記4 2026-08-22（ラベルの反転）**: ラベル抑止判定をアンカー実体へ緩めた結果、「枠は完全に見えているがその下のラベルはクリップの外」という位置が成立していた。`HighlightLabel` が自分の高さを実測し、下辺に収まらず上辺側に収まるときは枠線の上へ反転配置する（ユーザー裁定 2026-08-22）。
  - **追記 2026-08-22**: 「コ」の字の欠けは引き続き許容するが、**そこからラベルまで落ちるのは是正した**（ユーザー指摘「マスクの影響でラベルが見えていない」）。`renderOutline` のラベル抑止判定を `box`（アンカー±`paddingPx`）から**アンカー実体の矩形**へ移した。リングが削れただけではラベルを消さず、アンカー自身が一部でも隠れた時点で消す。これで「半端に切れた文言を出さない」という当初の意図は保ったまま、クリップ端に接するだけの完全可視アンカーでラベルが必ず消える問題が無くなる。あわせて `ItemListPanel` の `ScrollArea.Autosize`（内容ぴったりに縮む＝クリップ矩形が1段分しかない）を、パネル本文いっぱいに広がる `ScrollArea` へ変更した。
- **完全に隠れていても ack は `ready` のまま**で、Unity 側は「表示できている」と認識し続ける。描画はされないため見た目の不整合は無いが、契約上の不整合は残る。
- `dragGuide` は従来どおりパネル外へはみ出したまま残る。
- **追記 2026-08-22: 同じ穴がMantine `Tooltip` にも空いていた。** 本ADRはチュートリアルのハイライトだけを塞いだが、スロットのホバーツールチップも Portal へ出るため祖先の overflow が効かず、一覧をスクロールすると内容と一緒に滑ってパネルの外まで出ていた（ユーザー指摘）。ツールチップは毎フレーム追従する要素ではないためマスクではなく**祖先がスクロールしたら引っ込める**契約とし、`shared/ui/HoverTooltip` に集約した（`opened` を自前で持つ。`disabled` で畳むと Mantine のホバー状態ごと消え、ポインタが同じセルに載ったままだと二度と開かない）。「要素に紐づく Portal オーバーレイは祖先スクローラに対する扱いを必ず決める」を webui-design §8 の規約として明文化した。
- **クリップ祖先自身の幾何変化の購読は、window resize・DOM属性変異・スクロールに限定される。** `TutorialAnchorRegistry` はウィンドウの `resize`・`document` の `scroll`（capture）・`MutationObserver`（`childList`/`subtree`/`attributes`）・`visualViewport` の `resize`/`scroll` を購読し、アンカー自身の矩形は `ResizeObserver` でも監視する。しかし監視対象はアンカー要素そのものであり、それを包むクリップ祖先（研究ツリーのパネル等）の幅・高さ変化は個別に監視していない。抜けているのは「ウィンドウは変わらないが祖先パネルの内部だけがCSSトランジション等で幅・高さを変える」経路（現行UIには該当する入力は無い）で、これが起きると再解決が走らず古いマスクのままハイライトがはみ出しうる。
- **`renderOutline` の戻り値契約が変わった。** 従来は「`status: "ready"` ⇒ `[data-kind="outline"]` の DOM 要素が存在する」だったが、祖先クリップとの交差が空のとき `null` を返して要素ごと描かなくなったため、正しくは「ready かつ可視 ⇒ 要素が存在する」である。DOM存在をready の代理として見ているテスト・監視コードはこの契約変更を踏まえること。

出所: ユーザー裁定 2026-08-20「一旦そこ許容して実際の見た目をしりたい」・2026-08-20 レビュー裁定（C7実測・D2案A/W17）
