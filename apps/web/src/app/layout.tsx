import type { Metadata } from "next";
import { Fraunces, Instrument_Sans, Spline_Sans_Mono } from "next/font/google";
import type { ReactNode } from "react";
import { Providers } from "@/app/providers";
import "./globals.css";

const fraunces = Fraunces({ subsets: ["latin"], variable: "--font-fraunces", display: "swap" });
const instrument = Instrument_Sans({ subsets: ["latin"], variable: "--font-instrument", display: "swap" });
const splineMono = Spline_Sans_Mono({ subsets: ["latin"], variable: "--font-splinemono", display: "swap" });

export const metadata: Metadata = {
  title: { default: "FieldLedger", template: "%s · FieldLedger" },
  description: "The record book for every acre.",
};

export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <html lang="en" className={`${fraunces.variable} ${instrument.variable} ${splineMono.variable}`}>
      <body><Providers>{children}</Providers></body>
    </html>
  );
}
