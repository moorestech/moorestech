import { itemIconUrl } from "@/bridge";
import GameIcon from "./GameIcon";
import { useI18n } from "@/shared/i18n";

type Props = {
  itemId: number;
  alt?: string;
  className?: string;
};

export default function ItemIcon({ itemId, alt, className }: Props) {
  const { t } = useI18n();
  return <GameIcon id={itemId} src={itemIconUrl(itemId)} alt={alt ?? t("item {itemId}", { itemId })} className={className} />;
}
