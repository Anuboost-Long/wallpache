import { Analytics } from "@vercel/analytics/next";
import type { Metadata, Viewport } from "next";
import { Bricolage_Grotesque, Geist, Geist_Mono } from "next/font/google";

import { Aurora } from "@/components/layout/Aurora";
import { ScrollHashCleaner } from "@/components/layout/ScrollHashCleaner";
import { SiteFooter } from "@/components/layout/SiteFooter";
import { SiteHeader } from "@/components/layout/SiteHeader";
import { site } from "@/lib/site";

import "./globals.css";

const geistSans = Geist({ variable: "--font-geist-sans", subsets: ["latin"] });
const geistMono = Geist_Mono({ variable: "--font-geist-mono", subsets: ["latin"] });
// A wonkier grotesque for headings and the wordmark — body copy stays on
// Geist for legibility, but anything shouting about the product gets some
// personality, echoing a wallpaper that's allowed to move and misbehave.
const bricolage = Bricolage_Grotesque({
  variable: "--font-bricolage",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  metadataBase: new URL(site.url),
  title: `${site.name} — ${site.tagline}`,
  description: site.description,
  icons: { icon: "/favicon.png", apple: "/icon-512.png" },
  openGraph: {
    title: `${site.name} — Live video wallpapers`,
    description: site.description,
    images: ["/icon-512.png"],
    type: "website",
  },
  twitter: { card: "summary_large_image" },
};

export const viewport: Viewport = { themeColor: "#0B0718" };

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="en"
      className={`${geistSans.variable} ${geistMono.variable} ${bricolage.variable} h-full antialiased`}
    >
      <body className="flex min-h-full flex-col">
        <Aurora />
        <ScrollHashCleaner />
        <SiteHeader />
        <main className="flex-1">{children}</main>
        <SiteFooter />
        {/*
          Page views only — no custom events, no cookies. The script is served
          from `/_vercel/insights/script.js`, which only exists on Vercel: on
          any other static host it 404s and nothing is collected.
        */}
        <Analytics />
      </body>
    </html>
  );
}
