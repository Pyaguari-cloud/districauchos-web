"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import type { Promo } from "@/content/promos";
import { ChevronIcon } from "./icons";

const INTERVAL = 6000;

/**
 * Pantalla de ofertas y comunicados del inicio (pedido del cliente 2026-11-24). Buenas prácticas:
 *  - Se desliza con el dedo (scroll-snap nativo), con flechas, con los puntos o con el teclado (← →).
 *  - Avanza sola cada 6 s, pero se detiene al pasar el mouse, al enfocar algo dentro, si la pestaña no se ve, si la
 *    persona eligió "reducir movimiento", o con el botón de pausa (requisito WCAG 2.2.2 para contenido que se mueve).
 *  - Un video se reproduce solo cuando está en pantalla; mientras dura no avanza, y al terminar pasa al siguiente.
 */
export function PromoCarousel({ slides }: { slides: Promo[] }) {
  const track = useRef<HTMLDivElement>(null);
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false); // lo eligió la persona
  const [hover, setHover] = useState(false);
  const [reduced, setReduced] = useState(false);
  const [muted, setMuted] = useState(true);
  const videos = useRef<Record<number, HTMLVideoElement | null>>({});
  const n = slides.length;

  useEffect(() => {
    const m = window.matchMedia("(prefers-reduced-motion: reduce)");
    const set = () => setReduced(m.matches);
    set();
    m.addEventListener("change", set);
    return () => m.removeEventListener("change", set);
  }, []);

  const go = useCallback((i: number) => {
    const el = track.current;
    if (!el || n === 0) return;
    const next = (i + n) % n;
    el.scrollTo({ left: next * el.clientWidth, behavior: reduced ? "auto" : "smooth" });
  }, [n, reduced]);

  // El índice sale de la posición real (también cuando desliza con el dedo).
  const onScroll = () => {
    const el = track.current;
    if (el) setIndex(Math.round(el.scrollLeft / el.clientWidth));
  };

  const current = slides[index];
  const isVideo = current?.kind === "video";

  // Avance automático (no en videos: esos avanzan al terminar).
  useEffect(() => {
    if (n < 2 || paused || hover || reduced || isVideo) return;
    const t = window.setTimeout(() => { if (!document.hidden) go(index + 1); }, INTERVAL);
    return () => window.clearTimeout(t);
  }, [index, n, paused, hover, reduced, isVideo, go]);

  // Solo corre el video que está en pantalla.
  useEffect(() => {
    for (const [k, v] of Object.entries(videos.current)) {
      if (!v) continue;
      if (Number(k) === index && !paused) v.play().catch(() => {});
      else v.pause();
    }
  }, [index, paused]);

  if (n === 0) return null;

  const btn = "flex h-11 w-11 items-center justify-center rounded-full bg-black/60 text-white backdrop-blur hover:bg-black/80";

  return (
    <section aria-roledescription="carrusel" aria-label="Ofertas y comunicados"
      className="relative overflow-hidden rounded-2xl bg-brand-ink text-white"
      onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
      onFocus={() => setHover(true)} onBlur={(e) => { if (!e.currentTarget.contains(e.relatedTarget)) setHover(false); }}
      onKeyDown={(e) => { if (e.key === "ArrowRight") go(index + 1); if (e.key === "ArrowLeft") go(index - 1); }}>
      <div ref={track} onScroll={onScroll}
        className="no-scrollbar flex h-full snap-x snap-mandatory overflow-x-auto overscroll-x-contain">
        {slides.map((s, i) => (
          <div key={s.id} role="group" aria-roledescription="promoción" aria-label={`${i + 1} de ${n}`}
            aria-hidden={i !== index} inert={i !== index}
            className="relative aspect-[4/3] w-full shrink-0 snap-start sm:aspect-[16/9] lg:aspect-auto lg:h-[36rem]">
            <Slide s={s} first={i === 0} muted={muted}
              videoRef={(v) => { videos.current[i] = v; }}
              onEnded={() => (n > 1 ? go(i + 1) : videos.current[i]?.play().catch(() => {}))} />
          </div>
        ))}
      </div>

      {n > 1 && (
        <div className="pointer-events-none absolute inset-x-0 bottom-0 flex items-center justify-between gap-2 p-3 [&>*]:pointer-events-auto">
          <div className="flex gap-2">
            <button type="button" className={btn} onClick={() => go(index - 1)}>
              <ChevronIcon dir="left" /><span className="sr-only">Promoción anterior</span>
            </button>
            <button type="button" className={btn} onClick={() => go(index + 1)}>
              <ChevronIcon /><span className="sr-only">Promoción siguiente</span>
            </button>
          </div>
          <ol className="flex items-center gap-0.5 rounded-full bg-black/50 px-1.5 backdrop-blur">
            {slides.map((s, i) => (
              <li key={s.id}>
                <button type="button" onClick={() => go(i)} aria-current={i === index}
                  className="flex h-9 w-7 items-center justify-center">
                  <span className={`block h-2 rounded-full transition-all ${i === index ? "w-5 bg-brand-yellow" : "w-2 bg-white/60"}`} />
                  <span className="sr-only">Ir a la promoción {i + 1}</span>
                </button>
              </li>
            ))}
          </ol>
          <div className="flex gap-2">
            {isVideo && (
              <button type="button" className={btn} onClick={() => setMuted((m) => !m)} aria-pressed={!muted}>
                <span aria-hidden="true" className="text-lg">{muted ? "🔇" : "🔊"}</span>
                <span className="sr-only">{muted ? "Activar sonido" : "Quitar sonido"}</span>
              </button>
            )}
            <button type="button" className={btn} onClick={() => setPaused((p) => !p)} aria-pressed={paused}>
              <span aria-hidden="true" className="font-display text-base font-bold">{paused ? "▶" : "❚❚"}</span>
              <span className="sr-only">{paused ? "Reanudar" : "Pausar"} promociones</span>
            </button>
          </div>
        </div>
      )}
    </section>
  );
}

function Slide({ s, first, muted, videoRef, onEnded }: {
  s: Promo; first: boolean; muted: boolean; videoRef: (v: HTMLVideoElement | null) => void; onEnded: () => void;
}) {
  if (s.kind === "image") {
    // eslint-disable-next-line @next/next/no-img-element -- banner del cliente tal cual, sin recortar
    const img = <img src={s.src} alt={s.alt} loading={first ? "eager" : "lazy"} className="absolute inset-0 h-full w-full object-contain" />;
    return s.href ? <Link href={s.href} className="absolute inset-0">{img}</Link> : img;
  }
  if (s.kind === "video") {
    return (
      <>
        <video ref={videoRef} src={s.src} poster={s.poster} muted={muted} playsInline preload={first ? "auto" : "metadata"}
          onEnded={onEnded} aria-label={s.label} className="absolute inset-0 h-full w-full bg-black object-contain" />
        {s.href && (
          <Link href={s.href} className="absolute right-3 top-3 rounded-lg bg-brand-yellow px-4 py-2 font-display font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark">
            Ver productos
          </Link>
        )}
      </>
    );
  }
  return (
    <div className="absolute inset-0 flex items-center bg-[radial-gradient(circle_at_85%_30%,#2a2a2a_0%,#111_60%)]">
      <div aria-hidden="true" className="absolute inset-y-0 left-0 w-2 bg-brand-yellow" />
      <div className="relative z-10 flex max-w-[62%] flex-col gap-2 p-5 pb-16 sm:gap-3 sm:p-8 sm:pb-20">
        {s.kicker && <p className="font-display text-sm font-bold uppercase tracking-[0.2em] text-brand-yellow">{s.kicker}</p>}
        <p className="font-display text-2xl font-bold uppercase leading-[1.05] tracking-wide sm:text-4xl">
          {s.title} {s.highlight && <span className="text-brand-yellow">{s.highlight}</span>}
        </p>
        <p className="hidden text-sm text-gray-300 sm:block sm:text-base">{s.text}</p>
        <Link href={s.cta.href}
          className="mt-1 inline-flex min-h-11 w-fit items-center rounded-lg bg-brand-yellow px-4 font-display font-bold uppercase tracking-wide text-brand-black hover:bg-brand-yellow-dark sm:px-5 sm:text-lg">
          {s.cta.label}
        </Link>
      </div>
      {s.image && (
        <div aria-hidden="true" className="absolute right-[4%] top-1/2 flex aspect-square w-[34%] -translate-y-1/2 rotate-3 items-center justify-center rounded-2xl bg-white p-3 shadow-2xl">
          {/* eslint-disable-next-line @next/next/no-img-element -- foto WebP del cliente */}
          <img src={s.image} alt="" loading={first ? "eager" : "lazy"} className="max-h-full max-w-full object-contain"
            onError={(e) => { e.currentTarget.parentElement!.hidden = true; }} />
        </div>
      )}
    </div>
  );
}
