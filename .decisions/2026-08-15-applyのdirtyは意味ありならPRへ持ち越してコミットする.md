# applyのdirtyは意味ありならPRへ持ち越してコミットする

決定: pr-adjudicated-apply が dirty を「意味のある変更（コミット漏れ等）」と判定したら、stashせずPRブランチへ持ち越してそのままcommitし、PRに含める。何を持ち越したかは apply-result.json の summary に書く。判定は厳密さより続行を優先する（レアケースのため）

棄却案: git stash で退避しapply完了後に元ブランチでpop／元ブランチへWIP commitしてから続行／意味ありなら従来どおり中止

理由: レアケースに厳密な保全機構を積む価値がない。持ち越してcommitすれば少なくとも変更は失われず、PR差分として人間の目に触れる

リンク: [[2026-08-15-applyのdirty判定は全面エージェント判断にする]]
