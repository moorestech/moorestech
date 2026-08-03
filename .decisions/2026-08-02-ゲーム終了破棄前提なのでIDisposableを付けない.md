決定: このゲームは「メインメニューに戻る」を想定せず、ゲーム全体のライフサイクルはゲーム終了と同時に破棄される。よってゲーム寿命のオブジェクトに IDisposable を付けない。EquipmentHeldItemModel の IDisposable は削除する。
棄却案:
- 解放対象（CTS・購読・Addressableハンドル）を実際に保持しDisposeも完全なので存置する（レンズのガード判定）
理由: Disposeするタイミングが実際には無いのに IDisposable があると「どこかで破棄される前提・ライフサイクルが管理されている」という誤解を与える。無いことが「ゲーム終了まで生きる」を暗黙に示す。
リンク: PR #1095 独立レビューD3。moores-code-review speculative-abstraction レンズ§3のガードへ本原則の反映が必要（ハーネス側followup）
