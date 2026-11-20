"use client";

import { useEffect, useRef } from "react";
import "leaflet/dist/leaflet.css";
import { mapsLink, negocio, phoneDisplay } from "@/content/negocio";

/**
 * Mapa con un pin por local (Leaflet + OpenStreetMap: sin clave de Google ni cookies).
 * Al tocar un pin se abre el nombre, la dirección y "Cómo llegar" (Google Maps del local).
 */
export function StoresMap() {
  const el = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let map: import("leaflet").Map | undefined;
    let cancelled = false;
    (async () => {
      const L = await import("leaflet");
      if (cancelled || !el.current) return;
      map = L.map(el.current, { scrollWheelZoom: false });
      L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
      }).addTo(map);

      const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]!);
      const markers = negocio.stores.map((s) => {
        const icon = L.divIcon({
          className: "",
          html: `<div class="store-pin"><span>${esc(s.name)}</span></div>`,
          iconSize: [0, 0],
          iconAnchor: [0, 0],
        });
        return L.marker([s.lat, s.lng], { icon, title: `Local ${s.name}`, keyboard: true })
          .bindPopup(
            `<strong>${esc(s.name)}</strong> · ${esc(s.role)}<br>${esc(s.address)}<br>Tel. ${esc(phoneDisplay(s.phone))}<br>` +
              `<a href="${esc(mapsLink(s))}" target="_blank" rel="noopener noreferrer">Cómo llegar →</a>`,
          )
          .addTo(map!);
      });
      map.fitBounds(L.featureGroup(markers).getBounds().pad(0.08));
    })();
    return () => {
      cancelled = true;
      map?.remove();
    };
  }, []);

  return (
    <div
      ref={el}
      role="region"
      aria-label={`Mapa con los ${negocio.stores.length} locales`}
      className="h-96 w-full overflow-hidden rounded-xl border border-line bg-surface md:h-[28rem]"
    />
  );
}
