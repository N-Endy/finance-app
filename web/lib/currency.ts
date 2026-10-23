export function formatCurrency(amount: number, currencyCode = "USD"): string {
  const code = (currencyCode || "USD").toUpperCase();
  if (code === "USDT") {
    return `₮${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  try {
    return new Intl.NumberFormat(undefined, {
      style: "currency",
      currency: code,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(amount);
  } catch {
    const symbols: Record<string, string> = {
      USD: "$",
      EUR: "€",
      GBP: "£",
      NGN: "₦",
      CAD: "CA$",
      AUD: "A$"
    };
    const sym = symbols[code] ?? `${code} `;
    return `${sym}${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }
}

export function formatMinorUnits(minor: number, currencyCode = "USD"): string {
  return formatCurrency(minor / 100, currencyCode);
}
