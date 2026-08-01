# 改善キュー — pr-independent-review reconcile起票分

reconcileモードが見逃し（missed）から起票する改善作業の台帳。改善の実施・検証は
moores-code-review `references/skill-improvement.md`（3段階検証・eval fixtures追記）が単一の正で、
本ファイルは債務の追跡だけを担う。

- `状態` は `open` / `closed` の2値。**closedにできるのは `closed根拠` 列に検証完了の実体
  （3段階検証を通した観点名＋eval/fixtures追記、またはpytest緑のテスト名）を書けた時だけ**。
  観点ファイルへの追記だけではclosedにしない。
- 同種のmissedは1件に束ねてよい（束ねた元コメントは出所PRの `## 突き合わせ内訳` で辿れる）。
- Step 0.5の健全性1行は本ファイルの `open` 行数を表示する。

| ID | 起票日 | 出所PR | 内容 | 分類 | 流し先 | 状態 | closed根拠 |
|---|---|---|---|---|---|---|---|
| Q1 | 2026-08-02 | #1095 | 不要抽象の検出観点（単一実装interface・意味のないIDisposable・存在意義のないメンバー・不要コード。missed 5件を束ね） | レンズ盲点 | skill-improvement手順（新観点or既存レンズ実例追記） | open |  |
| Q2 | 2026-08-02 | #1095 | 新設publicメンバーの公開範囲最小化観点（参照元が自ファイル/デバッグのみのpublic。missed 2件を束ね） | レンズ盲点 | skill-improvement手順 | open |  |
| Q3 | 2026-08-02 | #1095 | 重複メソッドのカスケード掃引強化（C11検知系統の汎化・CreateEntry直返し） | レンズ盲点 | skill-improvement手順 | open |  |
| Q4 | 2026-08-02 | #1095 | 不能フォールバック掃引の全インスタンス化（C1同型をファイル横断で数え上げる規則） | レンズ盲点 | skill-improvement手順 | open |  |
| Q5 | 2026-08-02 | #1095 | async CancellationToken伝搬規律のreviewer観点 | reviewer盲点 | skill-improvement手順 | open |  |
| Q6 | 2026-08-02 | #1095 | try-catch較正: 境界根拠コメント実在を確定免除にしない。コメントの主張を許可リスト3種（外部プロセス/ネットワーク/外部JSONパース）と照合するcandidateへ降格 | 決定論較正 | deterministic_checks.py改修＋skill-improvement 3段階検証 | open |  |
| Q7 | 2026-08-02 | #1095 | 規範成文化: 初期化メソッドはInitialize固定・ctor→Initializeの記述順 | 規範初出 | AGENTS.md成文化→決定論チェック化を検討→3段階検証 | open |  |
| Q8 | 2026-08-02 | #1095 | 規範成文化: 時間計測はGameUpdaterティック加算のみ（Deltatime等禁止・現在ティック数プロパティ新設が前提）。決定論check（サーバGame配下のTime.deltaTime/DateTime系regex）＋Roslyn analyzer化は別途起票 | 規範初出 | AGENTS.md成文化＋deterministic_checks.py→3段階検証 | open |  |
| Q9 | 2026-08-02 | #1095 | 規範成文化: if分岐はメソッド直下の一箇所へ集約する書式 | 規範初出 | AGENTS.md成文化→3段階検証 | open |  |
| Q10 | 2026-08-02 | #1095 | 規範成文化: 命名（イベントは何が変わったか名前で分かること・プロトコル名は実処理と一致） | 規範初出 | AGENTS.md成文化orレンズ実例追記→3段階検証 | open |  |
| Q11 | 2026-08-02 | #1095 | 規範成文化: デバッグ/テスト専用public APIをプロダクションに残さない（Responses.cs例・analyzer化要望あり） | 規範初出 | AGENTS.md成文化→3段階検証 | open |  |
| Q12 | 2026-08-02 | #1095 | 規範成文化: `{ get; private set; }` は許容（既存「単純getter/setter禁止」規約とのニュアンス整理が必要） | 規範初出 | AGENTS.md追記（ユーザー確認推奨・既存規約と衝突） | open |  |
