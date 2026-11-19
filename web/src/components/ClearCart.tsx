"use client";

import { useEffect } from "react";
import { cart } from "@/lib/cart";

/** Vacía el carrito una vez que el pago quedó confirmado. */
export function ClearCart() {
  useEffect(() => cart.clear(), []);
  return null;
}
