/** Presentation helpers for usage numbers. */

const compact = new Intl.NumberFormat("en-US", { notation: "compact", maximumFractionDigits: 1 });

export function formatTokens(count: number): string {
  return compact.format(count);
}

/** Small costs keep more decimals so a few-cent run is not shown as $0.00. */
export function formatCost(usd: number): string {
  if (usd === 0) return "$0";
  return usd < 1 ? `$${usd.toFixed(4)}` : `$${usd.toFixed(2)}`;
}
