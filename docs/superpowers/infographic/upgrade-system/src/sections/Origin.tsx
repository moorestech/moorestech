export function Origin() {
  return (
    <section className="section" id="origin">
      <div className="eyebrow">01 — 出発点</div>
      <h2>半導体エリアの散らばったメモから始まった</h2>
      <p className="lead">
        V8 mod (<code>moorestechAlphaMod_8</code>) の nodeGraph には、EUV 露光・ICチップ生産まわりに
        アップグレード関連のメモが点在していた。これらは全部「機械に挿して確率・品質を変える」同じパターンだった。
      </p>

      <div className="card">
        <h4>nodeGraph に書かれていた構想（抜粋）</h4>
        <ul>
          <li><strong>品質モジュール</strong> — 重ねがけで高レベル ICチップの出現率を底上げ（レベル1〜N）</li>
          <li><strong>歩留まり改善モジュール</strong> — EUV 露光の「失敗パターン」率（既定20%）を下げる</li>
          <li><strong>チップアップグレードモジュール</strong> — ダイのレベル分布（Lv1:80% / Lv2:18% / Lv3:2%）を決める</li>
          <li><strong>各種アップグレードモジュールの解放</strong> — 研究ツリーでの段階解放</li>
        </ul>
      </div>

      <p>
        これらを半導体専用で個別実装するのではなく、<strong>全機械共通の汎用モジュール基盤</strong>として設計し直す、
        というのがこのシステムの狙い。半導体の品質モジュールは、その基盤の上に載る一適用例になる。
      </p>

      <div className="note-callout">
        <span className="tag">設計スタンス</span>
        moorestech の方針上、後方互換・パフォーマンス最適化・将来拡張性は考慮不要。
        「より良い設計と動く実装」を優先し、改善は後から行う前提で判断している。
      </div>
    </section>
  );
}
