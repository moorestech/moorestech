import styles from "./style.module.css";

// 両端フェードする水平罫線。面の有無を問わず情報階層を区切る
// Horizontal rule fading at both ends to separate information hierarchy with or without a panel face
export default function FadeRule() {
  return <div className={styles.rule} aria-hidden="true" />;
}
