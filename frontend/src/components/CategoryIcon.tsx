import {
  Feather,
  Truck,
  Container,
  Bike,
  Car,
  Bus,
  Anchor,
  Weight,
  AlertTriangle,
  type LucideIcon,
} from "lucide-react";

export const ICON_MAP: Record<string, LucideIcon> = {
  Feather,
  Truck,
  Container,
  Bike,
  Car,
  Bus,
  Anchor,
  Weight,
  AlertTriangle,
};

/** Offered in the category admin form's icon picker. */
export const ICON_CHOICES = Object.keys(ICON_MAP).filter((key) => key !== "AlertTriangle");

export function CategoryIcon({ iconKey, className }: { iconKey: string; className?: string }) {
  const Icon = ICON_MAP[iconKey] ?? AlertTriangle;
  return <Icon className={className} aria-hidden="true" />;
}
