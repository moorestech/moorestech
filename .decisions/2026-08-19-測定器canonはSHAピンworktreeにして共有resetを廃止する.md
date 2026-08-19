# 測定器canonはSHAピンworktreeにして共有resetを廃止する

決定: pr-independent-reviewの`$CANON`は、起動時に解決した`origin/master`のSHAで作る使い捨てworktree `skills-canon-<sha8>`（detached・作成後は不変・reset不要）とする。旧共有`skills-canon`への毎回`fetch→reset --hard→clean -fd`は廃止。古いピンはレビュー起動時に`.last-used`が24時間超のものを自動掃除する

棄却案: ①共有skills-canonのまま許容（masterがレビュー実行中に進むと物差しが差し替わったまま正常な顔で完走し、fail-closedですらない）②canon準備区間のみflock直列化（ロック競合は消えるが実行中の差し替わりは残る）

理由: レビュー並列化（2026-08-19裁定）で共有canonへの同時resetが現実の競合になった。SHAピンなら差し替わりが構造的に不可能で、2026-08-05裁定の意図（origin/master固定の再現可能な測定器）はそのまま保存される。moores-wtのmasterピン（pin-<commit8>）と同型の前例あり

リンク: [[2026-08-05-測定器はorigin-master固定の専用worktreeから読む]] [[2026-08-19-無人レビューはレビュー無制限並列とapplyスロットプールで並列化する]]
