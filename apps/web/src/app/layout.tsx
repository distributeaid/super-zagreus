import type { Metadata } from "next";
import { roboto, permanentMarker } from "@/lib/fonts";
import "./globals.css";

export const metadata: Metadata = { title: "Zagreus", description: "DistributeAid partner portal" };

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${roboto.variable} ${permanentMarker.variable}`}>
      <body className="font-sans text-da-blue">{children}</body>
    </html>
  );
}
