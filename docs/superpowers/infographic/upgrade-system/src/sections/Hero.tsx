export function Hero() {
  return (
    <header className="hero">
      <div className="kicker">moorestech / 設計仕様の図解</div>
      <h1>
        機械に挿す<span className="accent">アップグレードモジュール</span>システム
      </h1>
      <p className="hero-lead">
        全機械共通で「速度・生産性・省エネ・品質」を変えるモジュール基盤の設計。
        Factorio 型のスロットに、トレードオフとスロット数の制約で意思決定を生む。
        ブレインストーミング → 仕様 → 実装計画を、Codex 外部監査で 2 回叩いて固めた設計判断を追う。
      </p>
      <div className="hero-meta">
        <span className="chip"><span className="dot" style={{ background: 'var(--speed)' }} />Factorio 型スロット</span>
        <span className="chip"><span className="dot" style={{ background: 'var(--productivity)' }} />独立 ItemId 採用</span>
        <span className="chip"><span className="dot" style={{ background: 'var(--quality)' }} />2 レイヤー分離</span>
        <span className="chip"><span className="dot" style={{ background: 'var(--efficiency)' }} />Codex 監査 ×2</span>
      </div>
    </header>
  );
}
