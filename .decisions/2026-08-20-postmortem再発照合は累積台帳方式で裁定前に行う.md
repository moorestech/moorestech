決定: postmortemに再発照合工程Step 1.5を新設し、照合基盤はlogs repoの累積台帳registry.md(Step 6で追記必須)とする。裁定提示に「再発判定: 初発/系統再発(前対策Xが理由Yで不発)」を必ず含め、系統再発時は新設でなく既存対策の修理を第一候補にする。初回ブートストラップは正式postmortem実行分=完全ブロック＋スキル導入以前の再発防止コミット=遡及簡易ブロック
棄却案: ①台帳を持たず毎回サブエージェントで4箇所(bd/moorestech較正コミット/~/.agents較正コミット/logs repo記録)を全量サーベイする ②bdの系統ラベル＋noteへ一本化する ③照合を裁定後の対策設計前(Step 3.0)に置く
理由: 記録が4箇所に散在し毎回サーベイは高コストで網羅性が揺れる。bd一本化はbd外のskill較正記録の取り込みが別途必要。「前対策があったのに効かなかった」事実は対策だけでなく裁定の帰責自体を変えるため照合は裁定前に要る
リンク: bd moorestech-t6z8 / .agents/skills/postmortem/references/recurrence-registry.md
