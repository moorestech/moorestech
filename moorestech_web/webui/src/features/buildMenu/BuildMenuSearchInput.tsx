import { useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

type Props = { value: string; onChange: (value: string) => void };

// §8.9様式の検索入力。素input+トークン背景でMantine TextInputを使わない
// §8.9 search input: bare input with token background, no Mantine TextInput
export function BuildMenuSearchInput({ value, onChange }: Props) {
  const { t } = useI18n();
  return (
    <input
      className={styles.searchInput}
      type="text"
      value={value}
      placeholder={t("検索")}
      onChange={(e) => onChange(e.currentTarget.value)}
      data-testid="build-menu-search"
    />
  );
}
