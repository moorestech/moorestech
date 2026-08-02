決定: Beadsのsync.remote（refs/dolt/data）とissues.jsonlミラーの退避先はprivateのmoorestech_logs repoとする
棄却案:
- public本体repoのremoteへ同期（tara-tari方式）(AIが大量に書くnoteの誤爆秘密情報が公開かつGitHub UIから不可視の場所に載り、Dolt履歴からの完全削除も困難)
- 同期なしローカルのみ (複数マシン・将来の共同作業を捨てることになり、以前Beadsを棄却した可搬性理由と矛盾)
理由: 蒸留済みで規律を効かせられる.decisionsと違い、bdのnoteは自由文が大量に書かれる前提。publicに置くリスクが構造的に高い。
リンク: [[2026-08-03-Beadsをタスクと設計と学びの台帳として導入する]]
