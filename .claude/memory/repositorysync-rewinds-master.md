---
name: repositorysync-rewinds-master
description: RepositorySyncが.moorestech-external-revisions.jsonのピンを根拠にmoorestech_masterをdetached HEADへ巻き戻す — masterデータ更新時はピン更新コミットと再checkout確認が必須
metadata: 
  node_type: memory
  type: project
  originSessionId: 8d9abf0a-acc4-439f-a03e-a23b76bc1c57
---

moorestech本体の `.moorestech-external-revisions.json` に記録されたcommitHashを根拠に、RepositorySync（Unity起動/同期時に走る）が `../moorestech_master` を**そのハッシュへ`git checkout`（detached HEAD化）する**。

2026-07-03の電力ワイヤーシステム検証で2回発生：masterに新データ（electricWireItems）をコミットしても、ピンが旧ハッシュのままだと同期時に巻き戻され「マスタロード例外でゲーム起動不能」になる。症状は「投入したはずのマスタデータが消えている」に見えるが、実体はdetached HEADへのcheckout（コミットはmasterブランチに健在、`git reflog`で確認可能）。

**Why:** ピンファイルは作業ツリー上のstale状態でも参照されるため、masterデータ更新→ピン未更新の窓で必ず巻き戻りが起きる。

**How to apply:** moorestech_masterへコミットしたら (1) 本体の `.moorestech-external-revisions.json` の commitHash を新ハッシュに更新してコミット (2) Unity起動・PlayMode前に `git -C ../moorestech_master branch --show-current` がmasterで最新かを確認、detachedなら `git checkout master` で復旧。関連: [[mooreseditor-owns-master-json]]（こちらはmooreseditor起動中の書き戻し、別機構）。
