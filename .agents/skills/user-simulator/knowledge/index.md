# 知識目録（L1）— 判事はdocを読んだ後、合致トリガーのあるものだけ開く

複製禁止の原則: 既存スキルに成文化済みの知識はポインタで参照する（情報は一箇所に集中）。

## 検査観点（lenses

| ファイル | 開くトリガー |
|---|---|
| lenses/complexity-containment.md | 戻り値の引き回し・共有可変インスタンス授受・型switch・中間構造の新設が設計に含まれる |
| lenses/ssot-data-placement.md | データ/設定/リソースの置き場・導出・複製・新ストア/新経路の新設が設計に含まれる |
| lenses/premise-verification.md | 既存仕様・データ意味・既存機構への依存の上に設計が載っている（裏取り確認） |
| lenses/scope-resolution.md | A/B案併記・YAGNI削り・二経路併存・「含める/含めない」の境界があいまい |
| lenses/blast-radius-enumeration.md | 【plan】既存型/プロパティ/ファイルの変更・リネーム・削除を含む |
| lenses/verification-coverage.md | 【plan】検証手順がある（モック偏重・デバッグ表示頼み・異常系欠落を見る） |

## 過去裁定コーパス

**コーパス2本の実体は private ログrepo にある**（コミット `53e9363d9` でrepo外へ移設済み。
パスは moorestech リポジトリルートからの相対で `../moorestech_logs/harness/user-simulator/knowledge/`）。
このディレクトリ内には存在しないので、下表のファイル名だけで探して「無い」と諦めないこと。

| ファイル | 開くトリガー |
|---|---|
| ../moorestech_logs/harness/user-simulator/knowledge/adjudications.md | 常時（薄いので毎回読む。ユーザーの裁定傾向の第一参照） |
| ../moorestech_logs/harness/user-simulator/knowledge/deviation-cases.md | 質問がプレイヤー操作/UI/UX・定数や個数の置き場・無効データの扱い・ID/キー体系・作業分割/先送りに触れるとき（推奨逸脱の具体事例＋確信度規律。深層） |

## 既存資産へのポインタ（複製しない）

| 場所 | 開くトリガー |
|---|---|
| ../../moores-grill-with-docs/references/moorestech-principles.md | moorestech固有の設計対話（B判定照合表: 冪等・ドメイン所有・プロトコル1本化・契約一般形・対称interface等） |
| ../../moores-code-review/references/lens-digest.md | 実装前チェックリスト相当の設計原則が要るとき |
| auto-memory（パスは環境依存のためここに書かない。contextファイルに記載される） | プロジェクト固有の暗黙知（仮置き機構・移行事情等）が関わりそうなとき。メインセッションがreview起動時にcontextへ関連メモリの抜粋かパスを含める |
