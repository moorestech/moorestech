import styles from "./style.module.css";

// 両端フェードする水平罫線。パネル内のセクション区切り用
// Horizontal rule fading at both ends, for section dividers inside panels
export default function FadeRule() {
  return <div className={styles.rule} aria-hidden="true" />;
}
