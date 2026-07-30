# 採掘進捗HUDの統合と再設計

## 目的

マップオブジェクトへフォーカスしたときの `Mining Target: ...` 表示を撤去する。採掘中の進捗表示は1本へ統合し、ホットバーと重ならず、ホットバーの寸法およびWeb UIデザイン哲学に沿う外観へ変更する。

## 現状

採掘進捗には2つのWeb表示経路がある。

1. `MapObjectMiningMiningState → ProgressBarView → ui.progress → ProgressBar`
2. `MapObjectMiningController → ui.mining_hud → MiningHud`

後から追加された `MiningHud` は対象名と2本目の進捗バーを描画する。既存の `ProgressBar` は画面下端から固定距離へ置かれ、ホットバーの番号タブと重なる。また、Mantineの緑色ゲージは `webui-design` が定める `GaugeBar` の半透明ネイビー溝・寒色グレー充填に一致しない。

## 検討した方式

1. **`ui.progress` へ統合し、`MiningHud` を撤去する（採用）**
   - 既存の採掘進捗の値源をそのまま使い、重複するtopic・DTO・controller公開メソッド・Web表示を削除できる。
   - `Mining Target` と重複バーを構造的に再発不能にできる。
2. **`ui.mining_hud` を残し、`ProgressBarView` の採掘利用をやめる**
   - チュートリアルアンカーを保ちやすいが、同じ採掘進捗のために追加された100msポーリングtopicと公開メソッドが残る。
3. **2経路を残して片方をCSSで隠す**
   - 変更量は少ないが、不要な通信と状態経路が残り、表示条件の変更で二重表示が再発する。

## 設計

`MiningHud` のReact feature、`ui.mining_hud` のWeb契約とUnity topic、専用DTO、controllerの専用読み取りメソッドを削除する。採掘状態が既に更新している `ProgressBarView` を唯一の進捗値源とし、Webでは `ProgressBar` だけを描画する。

`ProgressBar` はMantine `Progress` を使わず、既存の汎用 `GaugeBar` を使う。採掘時のC# `Label = null` は既存serializerによりwire上でキー省略となるため文字を描画しない。任意ラベルを持つ汎用契約は維持し、ラベルがある場合はゲージの上へ `--text-muted` で表示する。

配置は1280×720のstage内で `position: absolute` とする。ホットバーと同じ中央軸へ置き、幅は9スロットのホットバー全幅と一致させる。下端はホットバーの番号タブ上端から12px離し、ゲージ、空隙、番号タブ、スロットの順に読めるようにする。表示専用HUDとして `pointer-events: none` を維持し、z層は既存トークンだけを使う。

ホットバーのスロット寸法と間隔は共有CSSトークンへ昇格し、ホットバーと進捗バーの双方が同じ値を参照する。機能側へ新しい色を追加せず、ゲージの溝・充填・輪郭は `GaugeBar` と既存トークンへ委譲する。

`mining.hud` チュートリアルアンカーは、実ゲームで採掘だけが表示する統合後の `ProgressBar` へ移す。アンカーID自体は変更しない。

## 非目標

- 採掘時間、ダメージ、入力、アニメーション、効果音は変更しない。
- `ui.progress` の配信周期や `ProgressBarView` の状態管理は変更しない。
- ホットバーのスロット数、選択操作、見た目は変更しない。
- 新しい色、装飾、アニメーションは追加しない。

## 配置と前例

統合後のデータフローは `MapObjectMiningMiningState → ProgressBarView → ProgressTopic → ProgressBar → GaugeBar` とする。`ProgressTopic` は既存どおり `ProgressBarView.OnProgressChanged` を購読する表示用読み手であり、新しい状態、通信、駆動方向を追加しない。

| 項目 | 配置先 | 前例との一致 |
|---|---|---|
| 採掘進捗の実行時状態 | `Client.Game/InGame/UI/ProgressBar/ProgressBarView.cs` | 採掘stateが既にShow/Hide/SetProgressを明示駆動する既存経路を維持 |
| Webへの進捗配信 | `Client.WebUiHost/Game/Topics/ProgressTopic.cs` | `OnProgressChanged`購読と100msサンプリングを維持 |
| HUD描画 | `moorestech_web/webui/src/features/progress/ProgressBar.tsx` | 表示専用読み手として既存 `shared/ui/GaugeBar` を利用 |
| ホットバーとの相対寸法 | `app/index.css` と `HotbarPanel/style.module.css` の共有トークン | `webui-design` の固定長トークン原則とstage内絶対配置へ一致 |

`ui.mining_hud` は同じ進捗を別経路でポーリングする並行複製なので削除する。既存 `ui.progress` の駆動・購読を変えず、Web表示だけを差し替える受動的統合とする。

## 機能死活表

| 操作・表示 | 計画後 | 根拠 |
|---|---|---|
| マップオブジェクトへのフォーカス | 維持 | 採掘state machineとレイキャストは変更しない |
| `Mining Target: ...` 対象名表示 | 撤去 | ユーザーが明示的に削除を裁定 |
| 左クリック長押し中の進捗 | 維持 | 既存 `ProgressBarView` を唯一の値源として描画 |
| 採掘完了・中断時のバー非表示 | 維持 | 既存stateのShow/Hide呼び出しを変更しない |
| 採掘アニメーション・ダメージ・効果音 | 維持 | 対象コードを変更しない |
| `ui.progress` の任意ラベル表示 | 維持 | optional label契約と描画分岐を残す |
| ホットバーの選択・キー・ホイール操作 | 維持 | 表示専用バーは `pointer-events: none` で、操作コードを変更しない |

## 最も強い反例

任意ラベルを持つ別用途の進捗が将来表示された場合、ラベルを含むwrapper全体をホットバー幅へ収め、ゲージ下端をホットバーから12px離す。ラベルはゲージの上へ伸びるためホットバー側へ侵入しない。長いラベルは幅内で折り返さず省略し、ゲージ幅とホットバー幅の一致を壊さない。

基準解像度以外ではstage全体が一様に拡縮される。固定viewport座標ではなくstage内絶対配置と共有寸法トークンを使うため、ホットバーとゲージの相対幅・間隔は維持される。

## テストとQA

- 最初にPlaywrightテストを変更し、現状実装で次の理由により失敗することを確認する。
  - `Mining Target` または対象名が表示されている。
  - 進捗表示が重複している。
  - ゲージ幅がホットバー幅と一致しない。
  - ゲージとホットバー番号タブの間に12pxの空隙がない。
  - Mantineの緑色充填が使われている。
- mock hostの採掘シナリオを `ui.mining_hud` から `ui.progress` へ移し、`visible: true`、labelキー省略、0から1へ変化する進捗を配信する。固定の `Crafting` fixtureは採掘シナリオと分離し、旧mining-hud controlを削除する。
- 実装後、PlaywrightでDOM矩形を計測し、ゲージとホットバーの中心・幅が一致すること、両矩形が交差せず12px空くことを検証する。
- 採掘シナリオでは対象名が存在せず、`role="progressbar"` が画面内にちょうど1本だけ存在することを検証する。
- `role="progressbar"` の値、`pointer-events: none`、既存ゲージトークン由来の色をcomputed styleで検証する。
- mock hostで採掘状態を再現し、1280×720の全画面とホットバー周辺クロップを撮影する。対象名、重複、重なり、寸法、配色を目視し、問題が残れば修正と再撮影を繰り返す。
- Web UIの関連Vitest、Playwright、lint、buildを実行する。
- C#ファイル変更後に対象Unityテストと `uloop compile --project-path ./moorestech_client` を実行し、Errorログを確認する。
- `ui.mining_hud`、`MiningHud`、`Mining Target` の残存参照を検索する。

## 判断記録（ADR）

- **ユーザー裁定（発言「ok、playwrgihtで問題ないと言えるまで」、2026-07-27）**: `MiningHud` を撤去して `ui.progress` へ統合し、Playwrightで問題がないと言えるまで座標・外観を検証する。
- **agent前提（前例一致・真実源一元化原則、拒否権つき）**: 採掘進捗の値源は既存の `ProgressBarView → ui.progress` だけにし、重複する `ui.mining_hud` を契約ごと削除する。
- **agent前提（webui-design §8.6、拒否権つき）**: 進捗表示は既存 `GaugeBar` を使い、半透明ネイビーの溝、寒色グレーの充填、既存輪郭トークンだけで描画する。
- **agent前提（固定長トークン原則、拒否権つき）**: ゲージ幅を9スロットのホットバー全幅へ一致させ、番号タブ上端との空隙を12pxに固定する。
- **agent前提（既存契約の非目標、拒否権つき）**: `ui.progress` の `label?: string` 契約は維持し、採掘時はC# nullをwire上で省略する既存serializer挙動に合わせる。
- **agent前提（Playwrightの状態同一性、拒否権つき）**: mock hostの採掘シナリオも統合先の `ui.progress` へ移し、本番と同じラベルなし採掘状態を検証する。
- **機構比較**: `ui.mining_hud` への乗り換えやCSS非表示ではなく、既存のイベント駆動 `ui.progress` を唯一の経路にして重複機構を削除する。
