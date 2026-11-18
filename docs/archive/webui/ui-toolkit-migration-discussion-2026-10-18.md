# cef-unity を撤去し UI Toolkit へ移行する議論の記録（2026-10-18）

本書は 2026-10-17〜18 のユーザーと agent の議論を、裁定に至った論点ごとに整理したもの。
裁定本文は `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`。移行計画は別途 grill と plan で作る。

## 結論

- cef-unity（別プロセス Chromium + GPU テクスチャ共有）を撤去し、UI を素の C#/UXML/USS（UI Toolkit）へ全面移行する。
- 行き先として OneJS（UI Toolkit 描画 + TS）は経由しない。
- 決め手は「数万の配布環境で安定して動く構造」を「開発ループの速さ」より優先すること。

## 現状（2026-10-17 時点の実測）

| 項目 | 値 |
| --- | --- |
| Web UI 実装 | React 18 + Mantine 8 + zustand、ts/tsx 394 ファイル・約 24.5k 行、CSS 約 3.3k 行 |
| C# ホスト（`Client.WebUiHost`） | 134 ファイル・約 10.1k 行（Kestrel 配信・topic/action・CEF ナビゲータ） |
| Editor 無しで回るテスト | vitest 139 ファイル 809 ケース、Playwright 52 ファイル 211 ケース |
| Unity | 6000.3.8f1、manifest に `com.singtaa.onejs`（onejs-v3）が残存 |
| UI の系統 | uGUI は 2026-08-17 裁定で撤退済み。CEF 経由の Web UI が唯一の UI |

## cef-unity の既知問題（repo 内文書より）

- `docs/webui/cef-unity-frame-rate-followup-2026-07-29.md`: 外部負荷で全画面同期 Metal コピーが 300〜440ms に爆発し約 1fps へ崩壊。busy-wait・IPC burst・+3/+6ms flush・1000Hz pump・全画面コピー・リソース寿命・damage-streak 発振の 7 機構のうち、上流 101 コミットで修正されたのは 1 機構のみ。
- 同文書: Play 停止ごとに cef-unity-server がゾンビ化、Mach receive port が 1 サイクル +1 リーク、server が Unity の FD（ゲームサーバーの LISTEN ソケット含む）を継承。
- `docs/development/macmini/unity-editor.md`: Steam 版と Editor が CEF キャッシュを共有し `Init failed (code -6)` で Web UI が一切描画されない。
- `.decisions/2026-08-23-CEF未接続は別issueとしStep3は13-15で完了とする.md`: worktree で CEF 未接続のまま録画テストが落ちる。
- `docs/webui/TODO.md` A1/A2: CEF バイナリの LFS 配布、入力二重配送・IME・フォーカス排他が未完了。

## 論点と判断

### 1. 痛いのは Web という選択か、cef-unity というラッパーか

- agent の初期見解: 実測された不具合はすべて wrapper の実装品質と「別プロセスのブラウザを貼る」構造に由来する。Web 資産（24.5k 行、Editor 不要のテスト 1,020 ケース）と Mod 側から UI を差し替える動機を考えると、素の UI Toolkit への即決より、wrapper 修理の見積もりと OneJS（2026-05-21 に「ダメなら移行」と決めた退路）の試作比較を先にすべき。
- 採用経緯（Discord ミラーより）: 2026-02-09 に「uGUI と AI の相性は最悪だが UI Toolkit も全面採用できるほど洗練されていない」として Web 埋め込みへ傾き、05-21 に「ダメだったら OneJS に移行する前提で CEF 採用」と判断していた。

### 2. 「UI Toolkit は洗練されていない」は当時の AI 性能を前提にしていないか

- ユーザー: 当時の判断は当時の AI 性能が前提で、現在の AI なら素の C#/UXML/USS でも耐えられる。
- agent: 半分同意。
  - AI の向上で消える反論: 24.5k 行の書き直しコスト、Mantine 相当の部品自作コスト。UXML/USS は素のテキストで Prefab YAML を触らないため、uGUI 時代の「AI と相性最悪」の主因は当てはまらない。書き手が AI なら TS/React 層と C# の橋渡しは余計な層になり、OneJS を経由する理由は薄い。
  - AI の向上で消えない反論: 検証ループ（Editor 無しで回っていた 1,020 ケースが Unity Test Runner の裏に入る。律速は Editor 本数とドメインリロード）、フレームワークの穴（USS は transition のみで keyframe 無し、flexbox のみ、UI へのシェーダ効果ほぼ不可）、Mod 側から Unity を介さず UI を差し替える動機の消滅。

### 3. 検証ループの遅さは決め手になるか

- ユーザー: 検証ループは時間の問題で待てばよい。数万環境で安定して動く構造を取るべき。アニメーションは UI Toolkit 向けのフレームワークを導入または自作すればよい。
- agent: 同意。配布先が数万環境のゲームで、UI が別プロセスの Chromium と GPU テクスチャ共有に乗る構造はサポート面積として割に合わない。開発機 3〜4 台で既に Init 失敗・キャッシュ衝突・ゾンビ・FD 継承・LFS が出ており、GPU ドライバ・ウイルス対策・Steam オーバーレイ・非 ASCII パスが混ざる本番では再現も採取も難しい。UI Toolkit はレンダラと同じプロセス・同じ検証面なので「Unity が動く環境では UI も動く」という単純な保証になる。検証ループの遅さは払えるコスト、環境差で壊れる UI は払えないコスト。
- アニメーション: VisualElement の scheduler か style プロパティへのトゥイーンを薄く自作すれば通知・進捗演出の範囲は足りる。PrimeTween / LitMotion の UI Toolkit 対応は未確認で、導入するなら先に試す。

## 計画段階で押さえる点（未裁定。grill で決める）

- **移行中の出荷形**: パリティに届くまで CEF は配布物に残る。画面単位に UIState のルーティングで切り替える strangler 方式を agent は推す。C# 側の UIState と topic のデータモデルは正本として残し、Web への配信が C# 購読に変わるだけで書き直しにしない。
- **仕様の正本**: vitest 809 ケースと Playwright 211 ケースは各画面の挙動仕様が最も細かく書かれた資料。捨てる前に画面ごとの受入条件へ読み替え、Unity 側テストに移す。
- **完了の定義**: CEF 削除、Kestrel ホスト削除、`Client.WebUiHost` と `moorestech_web` の撤去、`CefRuntimeBundler` の撤去まで含めて完了とする。途中で止まると UI が 2 系統残る。
- **AI 向けの作法**: UI Builder 専用の状態を作らず UXML/USS をテキストで持つ。ビュー側 C# を薄く保ち、USS/UXML 変更ではドメインリロードを起こさない。VisualElement ツリーを PlayMode 無しの EditMode で組みクエリとイベント注入で検証する。見た目は uloop screenshot で agent が自己確認する。
- **第一弾の画面**: インベントリかホットバー（D&D・ツールチップ・i18n を含むため）。

## 関連

- `.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md`
- `.decisions/2026-08-17-クラフトUI改修はWebUI専任でuGUIは今後見ない.md`
- `docs/webui/MIGRATION.md`（uGUI→Web 移行の台帳。本裁定により Web→UI Toolkit の移行台帳へ置き換える対象）
