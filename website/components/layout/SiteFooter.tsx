import Image from "next/image";
import Link from "next/link";

import { legalLinks, navLinks, site } from "@/lib/site";

const externalLinks = [
  { href: site.releases, label: "Releases ↗" },
  { href: site.distRepo, label: "Dist repo ↗" },
  { href: site.installScript, label: "install.sh ↗" },
];

export function SiteFooter() {
  return (
    <footer className="border-t border-white/8 px-4 py-14 sm:px-6">
      <div className="mx-auto flex max-w-6xl flex-col items-center gap-7 text-center">
        <Link
          href="/"
          className="font-heading flex items-center gap-2.5 font-semibold tracking-tight"
        >
          <Image
            src="/wave-mark.png"
            alt=""
            width={640}
            height={263}
            className="h-6 w-auto"
          />
          {site.name}
        </Link>

        <nav
          aria-label="Footer"
          className="flex flex-wrap items-center justify-center gap-x-6 gap-y-3 text-sm text-white/55"
        >
          {navLinks.map((link) => (
            <Link key={link.href} href={link.href} className="transition hover:text-white">
              {link.label}
            </Link>
          ))}
          {externalLinks.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              target="_blank"
              rel="noopener noreferrer"
              className="transition hover:text-white"
            >
              {link.label}
            </Link>
          ))}
        </nav>

        <p className="max-w-md text-sm leading-relaxed text-white/40">
          Wallpache is not open source — the public repo carries the installer and
          the builds. Your videos never leave your machine.
        </p>

        <nav
          aria-label="Legal"
          className="flex flex-wrap items-center justify-center gap-x-6 gap-y-2 text-xs text-white/40"
        >
          {legalLinks.map((link) => (
            <Link key={link.href} href={link.href} className="transition hover:text-white/70">
              {link.label}
            </Link>
          ))}
        </nav>

        <p className="text-xs text-white/30">
          © {new Date().getFullYear()} {site.copyrightHolder}. All rights reserved.
        </p>
      </div>
    </footer>
  );
}
