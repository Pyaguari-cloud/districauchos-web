/** Respuesta de /api/cotizar (la calcula la API .NET, brief §8). */
export type QuoteLine = {
  oem: string; code: string; title: string; qty: number; pvp: number; unitPrice: number;
  mayorApplied: boolean; lineTotal: number; available: boolean; showCode: boolean;
  /** Unidades que salen de inventario y las que quedan bajo pedido (p. ej. pide 5, hay 3 → 3 + 2). */
  inStock: number; backorderQty: number; backorder: boolean;
};
export type QuoteResponse = {
  lines: QuoteLine[]; missing: string[]; subtotalPvp: number; total: number; hasVch: boolean;
  mayorApplied: boolean; faltaParaMayor: number; ahorro: number; umbral: number; baseMayor: number;
  delivery: Delivery; shipping: number; grandTotal: number; message: string;
  whatsappUrl: string | null;
};

export type Delivery = "none" | "pickup" | "quito" | "national";
