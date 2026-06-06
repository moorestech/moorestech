import { FigureFrame } from '../components/comment-feature';

export function CoreMechanic() {
  return (
    <section className="section" id="core">
      <div className="eyebrow">02 — 核となる仕組み</div>
      <h2>Factorio 型スロット ＋ 制約で「選択」を生む</h2>
      <p className="lead">
        機械ブロックが N 個のモジュール専用スロットを持ち、そこにモジュールアイテムを挿すと挙動が変わる。
        挿している間だけ効果が続き、抜けば戻る。スロット数はマスタで機械ごとに設定する。
        ただし効果は<strong>処理サイクル単位</strong>で、開始時のスロット内容をスナップショットして固定し、
        抜き差しは次サイクルから反映する（理由は §06）。
      </p>

      <FigureFrame label="図01 機械ブロックの構造。入力スロットと出力スロット（通常の入出力インベントリ）とは別枠で、専用のモジュールスロット行があり、そこに速度・生産性・省エネ・品質のモジュールを挿す。モジュールスロットへの通常搬入は禁止で、プレイヤーの明示装着のみ受け付ける">
        <svg viewBox="0 0 720 330" width="720" role="img" aria-label="機械ブロックのスロット構造">
          {/* machine body */}
          <rect x="40" y="40" width="640" height="250" rx="16" fill="#FFFFFF" stroke="#CBD5E1" strokeWidth="2" />
          <text x="60" y="72" fontSize="15" fontWeight="700" fill="#0F172A">機械ブロック（ElectricMachine / GearMachine）</text>

          {/* input slots */}
          <text x="60" y="108" fontSize="12" fontWeight="700" fill="#64748B">入力スロット</text>
          <rect x="60" y="118" width="44" height="44" rx="8" fill="#EFF6FF" stroke="#93C5FD" strokeWidth="1.5" />
          <rect x="112" y="118" width="44" height="44" rx="8" fill="#EFF6FF" stroke="#93C5FD" strokeWidth="1.5" />

          {/* arrow */}
          <text x="190" y="146" fontSize="22" fill="#94A3B8">→</text>

          {/* processing core */}
          <rect x="230" y="112" width="120" height="56" rx="10" fill="#F8FAFC" stroke="#475569" strokeWidth="1.5" />
          <text x="290" y="140" fontSize="12" fontWeight="700" fill="#334155" textAnchor="middle">レシピ処理</text>
          <text x="290" y="156" fontSize="10" fill="#64748B" textAnchor="middle">時間・消費・産出</text>

          <text x="378" y="146" fontSize="22" fill="#94A3B8">→</text>

          {/* output slots */}
          <text x="420" y="108" fontSize="12" fontWeight="700" fill="#64748B">出力スロット</text>
          <rect x="420" y="118" width="44" height="44" rx="8" fill="#ECFDF5" stroke="#6EE7B7" strokeWidth="1.5" />
          <rect x="472" y="118" width="44" height="44" rx="8" fill="#ECFDF5" stroke="#6EE7B7" strokeWidth="1.5" />

          {/* module slot row (separate) */}
          <rect x="60" y="196" width="600" height="74" rx="12" fill="#FBF7FF" stroke="#C4B5FD" strokeWidth="1.8" strokeDasharray="2 0" />
          <text x="74" y="220" fontSize="12" fontWeight="800" fill="#7C3AED">モジュールスロット（専用サブインベントリ・通常搬入禁止）</text>

          {[
            { x: 80, c: '#2563EB', t: '速度' },
            { x: 168, c: '#059669', t: '生産性' },
            { x: 256, c: '#F59E0B', t: '省エネ' },
            { x: 344, c: '#8B5CF6', t: '品質' },
          ].map((m) => (
            <g key={m.t}>
              <rect x={m.x} y="230" width="76" height="32" rx="8" fill="#FFFFFF" stroke={m.c} strokeWidth="2" />
              <text x={m.x + 38} y="251" fontSize="12" fontWeight="700" fill={m.c} textAnchor="middle">{m.t}</text>
            </g>
          ))}
          <rect x="436" y="230" width="40" height="32" rx="8" fill="#F1F5F9" stroke="#CBD5E1" strokeWidth="1.5" strokeDasharray="4 3" />
          <text x="456" y="251" fontSize="14" fill="#94A3B8" textAnchor="middle">＋</text>
          <text x="500" y="251" fontSize="12" fill="#64748B">空きスロット（有限）</text>
        </svg>
      </FigureFrame>

      <h3>「選択の著しさ」はトレードオフ ＋ スロット数で作る</h3>
      <p>
        スロットは有限。さらに各モジュールにトレードオフ（裏のコスト）を持たせる。
        だから「速度を盛るか、品質を盛るか」「ペナルティをどう相殺するか」がレイアウト最適化の判断になる。
        単純に強いだけのモジュールを無限に積める設計だと、意思決定が消えてただの作業になる。
      </p>
    </section>
  );
}
