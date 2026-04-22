# moorestech CEF Web UI 導入計画レポート

**期間**: 2026-03-12 〜 2026-04-20（Discord #Channel ログ）
**主要メンバー**: juhakurisu（実装主担当）/ sakastudio（プロダクトオーナー）/ toropippi（列車システム）

## 1. なぜやるのか（動機）

- **現行 uGUI の苦痛からの解放**: `HorizontalLayoutGroup` 地獄、多言語対応で煩雑な `TextMeshPro` 周り、レイアウト表現力の限界。
- **Web 技術スタック（React + Tailwind + TS）で UI を書きたい**: juhakurisu が試作したインベントリ UI（`cn()` + Tailwind クラスのみで 9 スロット横並び）を見て sakastudio が「全ゲームこれやるべき」と同意。
- **Mod 配布性**: UI が HTML/CSS/TS なら、Unity を介さず mod 側が UI を差し替えられる土壌になる（将来の脱 Unity mod 開発とも噛み合う）。
- **方針確定**: 有料アセット（Vuplex 3D WebView, Ultralight 商用ライセンス）は OSS/mod 配布時に足枷になるため、自前で CEF ベースを組む方向で一致。3D WebView は「中身参考のために買う」レベルの扱い。

## 2. これまでの技術検証サマリ

| フェーズ | 結果 | 備考 |
|---|---|---|
| CEF を Unity にアタッチしてビルド後も動かす | 成功 | 署名まわりは Steam 配布なら許容 |
| GPU テクスチャ共有（Mac） | 成功（色薄い問題あり） | `Texture2D.CreateExternalTexture` の sRGB 扱いが Unity 側バグ気味。srgb フラグ4通り全滅、同症例が Unity Discussion に複数あり |
| 色問題の回避 | シェーダーで blit して解決 | 本来は避けたかったが止むなし |
| 性能（Mac, 4K） | 雀魂相当のサイトが快適動作するレベルまで最適化 | ただし謎の 7ms 同期待ちが残存。Claude に自動ループ修正させたが「古フレームに気付かず完了と報告」で失敗、外部監査 (codex) プロンプトで目視精度を補強する運用に |
| Windows/Linux | 未着手 | Mac 最適化の一部は Windows では不要の可能性あり |
| 代替案調査 | Servo（Web 互換性が低い）/ Ladybird（JS ランタイムが弱い）/ FEF（廃止）/ Ultralight（WebKit 系）→ いずれも現時点で CEF を置き換えるには未成熟と結論 |
| コンソール/モバイル | 「CEF 魔改造 or 軽量ブラウザ魔改造」が必要で、moorestech 本編後の遠い課題 |

## 3. 実装方針（合意済みアーキテクチャ）

- **CEF をアプリ内 Web UI ランタイムとして同梱**。描画は GPU テクスチャ共有で Unity 側 `Texture2D` へ。
- **UI は TypeScript + React + Tailwind（pnpm 管理）**。
- **Unity ↔ Web 間の双方向 API** を策定し、C# のゲーム状態とサーバーイベントを Web に流す / Web の入力を C# へ戻す。
- **C# → TS への型定義を SourceGenerator で自動化**（既存の Mooresmaster モジュール方式の延長）。
- **アイテムアイコン等のアセット配信は ASP.NET 内蔵 HTTP サーバー案が有力**（Unity Addressables を Web 側から直接引けない問題の解決策）。
- **ガワ（CEF ラッパ）を抽象化**して、将来 WebKit / Servo / Ladybird への差し替え余地を残す。レンダラー差し替えは内部ニーズもあり現実的。
- **シングルプロセス化への関心**: 現状 CEF は「ゲーム + CEF」の 2 プロセス構成になる（CEF のシングルプロセスモードも別プロセス内で単一化されるだけ）。WebKit を直接組めばシングルプロセス・マルチスレッド化できる可能性を juhakurisu が趣味で追っている（稼働外）。
- **コピーレフト回避**: Firefox / Gecko 系はコピーレフト要素があり組み込み不可。配布バイナリをそのまま使う範囲では OK だが Embedded Framework 化すると汚染されるため除外。

## 4. タスクボード

### A. 基盤・プラットフォーム対応
- [ ] **Ultralight を試す** — WebKit ベースの軽量代替。商用ライセンス懸念はあるが、内部実装と単一プロセス性を検証目的で触る。`latacko/UltralightUnity` が既に存在。
- [ ] **音声出力先の変更の調査** — CEF が既定オーディオデバイスを占有する挙動への対処（ゲーム音声とのミックス課題）。
- [ ] **Windows, Linux 対応** — Mac で動いた最適化が Windows では不要な可能性。Linux は「余裕があれば」。
- [ ] **CEF リファクタ** — 実験コードのままなので、抽象化レイヤーを切って他ブラウザ差し替え可能な形に整理。
- [ ] **パッケージングして moorestech の repo に入れる** — 現状 CEF は別リポ/別環境で検証中。Unity プロジェクトに取り込める形でバイナリ同梱。

### B. フロントエンド基盤
- [ ] **ASP.NET の導入** — ローカル HTTP サーバーとして Unity プロセスに同居。アイテムアイコンや UI アセットを HTTP で供給。
- [ ] **pnpm、TS、React の導入** — UI 開発環境構築。Tailwind + `cn()` のスタックは試作で確認済み。
- [ ] **両者（Unity 側 / Web 側）の初期化パイプライン** — 起動順序、準備完了シグナル、CEF→Unity ハンドシェイク、アセットロード完了通知の設計。
- [ ] **C# to TS の SourceGenerator の開発** — マスタデータ・イベント型・API シグネチャを TS 型として自動生成。既存 Mooresmaster 自動生成と同じ思想。

### C. アセット・API 設計
- [ ] **UI Addressable Path をどうするか問題検討** — Web 側が Unity Addressables を直接引けないため、パス規約 or ASP.NET プロキシで吸収する設計判断が必要。
- [ ] **アイテムアイコンをどうやって運搬するか問題**（ASP.NET でいい説はある） — テクスチャを PNG として HTTP 経由で配信する方針が最有力。Web の `<img>` / CSS で扱えて最小コスト。
- [ ] **Unity ↔ Web 間の双方向 API の策定** — CEF の JS⇄Native ブリッジ上にイベント/RPC プロトコルを載せる。C#→TS SourceGenerator と同じ型定義を共有。

### D. 段階的移行
- [ ] **置き換え UI の洗い出し** — 現行 uGUI のどれから置き換えるかの棚卸し。インベントリ系が試作済みなので第一候補。ギア/エネルギー系 UI、デバッグ系は後段の可能性。

## 5. 未解決・リスク

- **Mac の 7ms 同期待ち**: 原因未特定。GPU 同期境界の疑い。Windows では再現しない可能性があるが要検証。
- **モバイル/コンソール**: 本編リリース後課題。wasm 上 Chromium / 軽量 Linux / syscall 仮想化のいずれも重量級調査。
- **mod 開発との整合**: Unity を使わない mod 開発を視野に入れる場合、prefab 不要の UI は CEF 側で完結でき親和性が高い一方、ブロック追加等では prefab が必要で別系統が残る。
- **AI 運用の落とし穴**: UI/グラフィックの自動修正ループで「古フレームに気付かず完了報告」事故があり、外部監査 (codex) プロンプトを噛ませる運用が確立済み。CEF 最適化フェーズでも必須。

## 6. 次アクションの推奨（ログから読み取れる優先度）

1. CEF リファクタ + Windows 対応（Mac で一旦満足、次は Windows と明言あり・2026-03-24）
2. パッケージング → moorestech repo 統合
3. ASP.NET + pnpm/TS/React 基盤導入（UI 実装側の土台）
4. 双方向 API と C#→TS SourceGenerator（型駆動で引き返しコストを抑える）
5. 置き換え UI の洗い出し → インベントリから順次移植
6. Ultralight 調査（趣味枠、代替レンダラー抽象化の具体要件抽出に活用）
