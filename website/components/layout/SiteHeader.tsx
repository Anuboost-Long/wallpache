"use client";

import clsx from "clsx";
import Image from "next/image";
import Link from "next/link";
import { useState } from "react";

import { ButtonLink } from "@/components/ui/ButtonLink";
import { GitHubIcon } from "@/components/ui/icons";
import { navLinks, site } from "@/lib/site";

export function SiteHeader() {
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <header className="fixed inset-x-0 top-0 z-50 px-4 pt-4 sm:px-6">
      {/* Scrim so content scrolling past the floating bar fades out instead of
          colliding with it */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-x-0 top-0 -z-10 h-28 bg-[linear-gradient(180deg,var(--color-ink)_15%,transparent)]"
      />
      <nav
        aria-label="Primary"
        className={clsx(
          "glass glass-rim relative mx-auto flex h-16 max-w-6xl items-center gap-4 rounded-2xl px-4 sm:px-5",
          // A sticky bar needs to stay legible over whatever scrolls under it
          "bg-ink/55 backdrop-blur-2xl",
        )}
      >
        <Link
          href="/"
          className="font-heading flex shrink-0 items-center gap-2.5 text-[1.05rem] font-semibold tracking-tight"
        >
          <Image
            src="/wave-mark.png"
            alt=""
            width={640}
            height={263}
            className="h-7 w-auto"
            priority
          />
          {site.name}
        </Link>

        <ul className="ml-4 hidden items-center gap-1 md:flex">
          {navLinks.map((link) => (
            <li key={link.href}>
              <Link
                href={link.href}
                className="rounded-full px-3.5 py-2 text-sm text-white/65 transition hover:bg-white/8 hover:text-white"
              >
                {link.label}
              </Link>
            </li>
          ))}
        </ul>

        <div className="ml-auto flex items-center gap-2">
          <ButtonLink
            href={site.releases}
            variant="ghost"
            size="sm"
            className="hidden sm:inline-flex"
          >
            <GitHubIcon className="size-4" />
            Releases
          </ButtonLink>

          <ButtonLink href="#download" size="sm" className="hidden sm:inline-flex">
            Get it free
          </ButtonLink>

          <button
            type="button"
            aria-expanded={menuOpen}
            aria-controls="mobile-menu"
            aria-label="Toggle menu"
            onClick={() => setMenuOpen((open) => !open)}
            className="glass-soft inline-flex size-10 items-center justify-center rounded-xl md:hidden"
          >
            <span className="relative block h-3 w-4.5">
              <span
                className={clsx(
                  "absolute inset-x-0 h-0.5 rounded-full bg-white transition",
                  menuOpen ? "top-1.5 rotate-45" : "top-0",
                )}
              />
              <span
                className={clsx(
                  "absolute inset-x-0 h-0.5 rounded-full bg-white transition",
                  menuOpen ? "top-1.5 -rotate-45" : "top-3",
                )}
              />
            </span>
          </button>
        </div>
      </nav>

      {menuOpen && (
        <div
          id="mobile-menu"
          className={clsx(
            "glass mx-auto mt-2 flex max-w-6xl flex-col gap-1 rounded-2xl p-3 md:hidden",
            "bg-ink/92 backdrop-blur-2xl",
          )}
        >
          {navLinks.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              onClick={() => setMenuOpen(false)}
              className="rounded-xl px-4 py-3 text-sm text-white/75 transition hover:bg-white/8 hover:text-white"
            >
              {link.label}
            </Link>
          ))}
          <Link
            href={site.releases}
            target="_blank"
            rel="noopener noreferrer"
            className="rounded-xl px-4 py-3 text-sm text-white/75 transition hover:bg-white/8 hover:text-white"
          >
            Releases on GitHub ↗
          </Link>
        </div>
      )}
    </header>
  );
}
