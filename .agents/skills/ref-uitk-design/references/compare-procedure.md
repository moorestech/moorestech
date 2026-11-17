# 見た目の比べ方（撮る・並べる・判定する・記帳する）

画面・部品を作った・直したとき、完了報告の前に開く。画素の一致は目標にしない。比べるのは位置・寸法・色・装飾の有無・文字の段組。

目次: 1 誰が・いつ・何を / 2 参照キャプチャ / 3 UITK を撮る / 4 並べ比べの載せ方 / 5 合否の目安 / 6 差の直し方と例外の記帳 / 7 直す前の実測と前提審査

出所: `docs/uitk-migration/execution/component-architecture.md` §3.3・§3.4、`origin/master:docs/uitk-migration/plan.md` §4.6。

## 1. 誰が・いつ・何を

| 何を | 誰が | いつ |
|---|---|---|
| UITK の部品の見本 | 部品レーン | 部品の L1 が通った後 |
| UITK の画面 | 画面レーン | 画面の L1 が通り、L3/L4 に入る前 |
| 変更の前後の並べ比べ | 作ったレーン | 完了報告の前 |
| ユーザーの目視 | ユーザー | 切替 PR の検証ビルドで 1 回だけ |

## 2. 参照キャプチャ

- 参照は `moorestech_logs/uitk/reference-captures/web-<sha>/` と `web-<sha>-2560x1080/`（撮影元は `docs/uitk/reference-captures.md`。画面と状態の一覧は `index.md`、機械向けは `manifest.json`）。Web 版は撤去したので撮り直せない
- 新しい画面には参照が無い。`visual-vocabulary.md` の約束で作り、変更の前後の UITK どうしを並べる

## 3. UITK を撮る

- **部品**: 部品ギャラリー（`Client.Ui.Editor` の EditorWindow。本番の tss を当て、部品ごとに全状態を並べる。部品レーンが `IUiGalleryEntry` を 1 つ足す）を開き、`uloop screenshot --project-path ./moorestech_client --capture-mode window --window-name <ギャラリーの題> --output-directory <worktree の絶対パス>/moorestech_client/.uloop/outputs/compare/<レーンID>` で撮る
- **画面**: PlayMode で、参照キャプチャと同じ状態を DSL のシナリオで作り、ScreenCapture で 1280×720 と 2560×1080 の 2 種を撮る
- `--capture-mode rendering`（PlayMode の既定）には UITK が写らない。撮れた画像は結果の `ImagePath` を開く（ディレクトリの新しい順で拾わない）
- 画面の ScreenCapture も同じ `moorestech_client/.uloop/outputs/compare/<レーンID>/` に置く
- **色はランタイムの PanelSettings で比べる**: GameUi は Web と同じ sRGB 空間で合成する（パネルを `forceGammaRendering` で RT に描き URP の最後に世界へ重ねる。設計書 [A-180]）ので、トークンの α は Web の値そのもの。色は下の撮影道具（本番の PanelSettings の複製を撮る）か PlayMode の画面の撮影で参照と比べる。部品ギャラリー（EditorWindow）も sRGB 合成で同じ見え方になるが、本番の設定を写さないので色の合否はランタイムで出す（設計書 [A-185]）

### 撮影の道具（使い捨ての手順を書かない）

- 計画を書くときは、比較の撮影の手順にこの節の道具を名指しで書く（実行計画 §6.2「計画に必ず書くこと」）。道具で撮れない状態（例: 参照の組に無い掴み絵）だけ、計画に理由を書いて使い捨てで撮り、道具の拡張を integrator へ依頼する

- **部品をランタイムの合成で PNG にする**（EditMode。PlayMode 中は止める）: `Client.Ui.Editor.Capture.UiRuntimePanelCapture`。倍率は本番の ScaleWithScreenSize が幅×高さから決める（2560×1080 なら 1.5）。地の色は参照の画素を 1 色で渡す。画素を数で見たいときは `Render(...)` が読める `Texture2D` を返す（左下原点。捨てるのは呼び手）。辞書は引数 `UiCaptureLocalization` で選ぶ（`InitializeDictionary` は撮る前に読み直す・`KeepCurrentDictionary` はテストで足した辞書を残す）。地と幕は引数 `UiCaptureBackdrop` で選ぶ（部品は `UiCaptureBackdrop.GroundColorOnly`。画面を参照キャプチャ（Web の DEMO）と並べるときは `new UiCaptureBackdrop(UiCaptureShell.VignetteAndDim, 地の画像)` で本番の `ScreenBackdropView`〈ヴィネット・幕〉を中身の奥に敷き、地の画像〈撤去前の Web 版の `git show 6248097123:moorestech_web/webui/public/mock-orange-gradient.png` で取り出した PNG を `Texture2D.LoadImage` で読んだもの・cover〉を地の色の代わりにする。幕は traits の Backdrop 列の画面だけ。画像の破棄は呼び手）。フォント資産を作り直した直後の Editor は太さ違いの字を描かないことがあるので、作り直したら Editor を起動し直してから撮る

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code 'return Client.Ui.Editor.Capture.UiRuntimePanelCapture.CaptureToPng(new Client.Ui.Editor.Gallery.Entries.Slots.ItemSlotGalleryEntry().CreateSample(), 2560, 1080, new UnityEngine.Color32(35, 29, 30, 255), Client.Ui.Editor.Capture.UiCaptureBackdrop.GroundColorOnly, Client.Ui.Editor.Capture.UiCaptureLocalization.InitializeDictionary, "<worktree の絶対パス>/moorestech_client/.uloop/outputs/compare/<レーンID>/itemslot-2560x1080.png");'
```

- **Game View の解像度を切り替える**: uloop では `uloop execute-dynamic-code --project-path ./moorestech_client --code 'return Client.Ui.Editor.Capture.GameViewResolution.Set(2560, 1080);'`（無ければ固定解像度を足して選ぶ。撮り終えたら 1280×720 へ戻す）。録画 DSL では `await PlaytestGameViewOps.SetResolution(p.CancellationToken, 2560, 1080);`（`using Client.Playtest.Operations.Ui;`。`Screen` が追いつくまで待つ）。例は `scenarios/uitk/integrator/uitk-gameview-resolution-switch.cs`
- 2560×1080 の参照は `web-0e81bbe3c3-2560x1080`（`docs/uitk/reference-captures.md`）

## 4. 並べ比べの載せ方

- 並べ比べは PR の成果物の載せ先（環境ローカル規約が決める。例: PR 報告ページ `https://review.moores.tech/pr/<番号>/report/`）に載せる。ページの書式は載せ先の生成器が決めるので、独自の HTML を書かない
- 1 状態につき参照（参照キャプチャか変更前の UITK）と変更後の UITK の画像を同じ `group`（状態名）に入れ、参照を先に置く。caption は `参照 <撮影 id か版>` と `UITK <状態名> 1280×720`（画面は `2560×1080` の組も足す）
- 差の表は作業報告 `moorestech_logs/harness/uitk/<レーンID>/done.md` に状態ごとに書く:

```markdown
### <状態名>（参照: <撮影 id か版> / UITK: <ファイル名>）

| 観点 | 参照 | UITK | 判定 | 対応 |
|---|---|---|---|---|
| 位置・寸法 | | | 同じ / 差 | 直した / LX-n |
| 色 | | | | |
| 装飾の有無 | | | | |
| 文字の段組 | | | | |
```

- 画像の原本は worktree の gitignore 下（`moorestech_client/.uloop/outputs/compare/<レーンID>/`）に置き、`moorestech_logs` には入れない（`moorestech_logs` に置くのは報告の md とスクショ 3 枚程度まで）

## 5. 合否の目安

- 1280×720 で位置・寸法の差が 2px 以内
- 色はトークン経由で同じ値
- 装飾（罫線・三角・影・ベベル・グラデ）の有無が同じ
- 文字の段組（折り返し・行数）が同じ
- 字形の差（Chrome と TextCore の描き方）は差に数えない

## 6. 差の直し方と例外の記帳

1. 差はまず参照キャプチャ・前の版へ寄せて直す（つまみの上書き漏れ・トークンの取り違え・半透明の補正漏れが多い）
2. 例外にしてよいのは、同じにするための対応が大きい（部品の作り直し・数百行の描画・性能の劣化）か、ブラウザの癖の再現で、かつ差が一目で分からないときだけ。配置・色の系統・装飾の有無・文字の大きさの差は例外にしない（ただし Web と同じ px で実機の比較で読めない文字は、撮影を添えて例外に記帳する。設計書 [A-39]）。直せないときは報告に「要裁定」と書いて止める
3. 例外は `docs/uitk/look-exceptions.md` の表に 1 行足す。ID は表の最大の番号の次を取る。列: ID（`LX-n`）／画面・部品／差／同じにしない理由／推奨案／撮影（報告ページの URL）／出所（agent 既定・裁定）
4. 例外にした USS・C# の行に `/* look-exception: LX-n */` を付ける。印と表の照合は `LookExceptionLedgerTest` が行う

## 7. 直す前の実測と前提審査

- 見た目の症状は数値の出所へ落としてから直す。スクショだけで原因を決めない。EDC で `worldBound`・`layout`・`resolvedStyle` と、スクロールの `contentContainer` と `contentViewport` の寸法を出す
- 原因の候補が複数あるときは ablation で切る（要素を 1 つずつ `display: none`・つまみを 1 つずつ変えて、症状が消える点を探す）
- 症状の値と一緒に器の値（パネル本文の高さ・親の矩形）を同じ出力に並べ、症状の箱（スクロール領域・クリップ矩形）が器と合っているかを先に読む。合っていなければ「この箱の大きさは何が決めるべきか（器・中身・正本）」を計画の項目にする（箱を直すと複数の症状が同時に消えることが多い）
- 既存の寸法の定数（固定 px・`--*-max-height`・`min-height`）は審査の対象で、所与ではない。据え置くなら「責務＝○○が決める値だから据え置く」と 1 行書く。「総量が変わらない」「N 段が収まる」は中身の量の話で、箱の大きさの責務の根拠にならない
- 同じ機構を使う兄弟（他のパネル・同じ層に載るオーバーレイ）を grep で列挙し、まとめて直すか、外すなら理由を書く。理由の無い「別 issue へ」は書かない。前例と形が違うなら違う理由を書く
