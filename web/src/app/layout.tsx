import type { Metadata, Viewport } from "next";
import { Barlow, Barlow_Condensed } from "next/font/google";
import { Footer } from "@/components/Footer";
import { GarageSync } from "@/components/GarageSync";
import { Header } from "@/components/Header";
import "./globals.css";

const barlow = Barlow({ subsets: ["latin"], weight: ["400", "500", "600", "700"], variable: "--font-barlow" });
const barlowCondensed = Barlow_Condensed({ subsets: ["latin"], weight: ["600", "700"], variable: "--font-barlow-condensed" });

const SITE_URL = process.env.SITE_URL ?? "https://districauchosvch.com";

export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: { default: "Districauchos · Repuestos de caucho automotriz en Quito", template: "%s · Districauchos" },
  description:
    "Bujes, mangueras, bases de motor y más repuestos de caucho para tu vehículo. Busca por nombre, vehículo o código OEM y cotiza por WhatsApp.",
  openGraph: { type: "website", locale: "es_EC", siteName: "Districauchos" },
};

export const viewport: Viewport = { themeColor: "#000000", width: "device-width", initialScale: 1 };

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="es-EC" className={`${barlow.variable} ${barlowCondensed.variable}`}>
      <body className="flex min-h-screen flex-col antialiased">
        <a href="#contenido" className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded focus:bg-brand-yellow focus:px-3 focus:py-2 focus:text-black">
          Saltar al contenido
        </a>
        <Header />
        <main id="contenido" className="mx-auto w-full max-w-6xl flex-1 px-4 py-6">
          {children}
        </main>
        <Footer />
        <GarageSync />
      </body>
    </html>
  );
}
