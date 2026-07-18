import { Roboto, Permanent_Marker } from "next/font/google";

/** Roboto (body font); exposes the `--font-roboto` CSS variable used by Tailwind's `font-sans`. */
export const roboto = Roboto({ subsets: ["latin"], weight: ["400", "500", "700"], variable: "--font-roboto", display: "swap" });

/** Permanent Marker (accent font); exposes `--font-permanent-marker`, used by Tailwind's `font-marker`. */
export const permanentMarker = Permanent_Marker({ subsets: ["latin"], weight: "400", variable: "--font-permanent-marker", display: "swap" });
