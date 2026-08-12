"use client";

import type { ReactNode } from "react";
import { useEffect, useRef, useState } from "react";

import { ButtonLink } from "@/components/ui/ButtonLink";
import { CopyCommand } from "@/components/ui/CopyCommand";
import { CloseIcon, DownloadIcon } from "@/components/ui/icons";
import { site } from "@/lib/site";

/**
 * The .dmg is ad-hoc signed, so the copy a browser hands over is quarantined
 * and macOS calls it "damaged" — which reads as a broken download rather than
 * as the missing Developer ID it actually is. The warning further down the page
 * only helps the people who read it, so the steps are put in front of everyone
 * who takes the .dmg, at the moment they take it.
 */

type Step = Readonly<{ title: string; body: ReactNode }>;

const steps: readonly Step[] = [
  {
    title: "Open Wallpache.dmg and drag Wallpache into Applications",
    body: "The usual drag-and-drop window. Then eject the disk image.",
  },
  {
    title: "Paste this into Terminal, then press Return",
    body: (
      <>
        <span className="block">
          Open Terminal with ⌘ Space, type <em className="not-italic text-white/70">Terminal</em>,
          Return. It prints nothing — that means it worked.
        </span>
        <CopyCommand command={site.quarantineCommand} wrap className="mt-3" />
      </>
    ),
  },
  {
    title: "Open Wallpache from Applications",
    body: "It opens normally from here on, and macOS never asks again.",
  },
];

export function MacDownloadButton() {
  const [open, setOpen] = useState(false);
  const closeRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) return;

    // Focus moves into the panel so Escape and Tab land somewhere sensible.
    closeRef.current?.focus();

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }

    // Restored rather than cleared, so the page's own overflow rule comes back.
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    document.addEventListener("keydown", onKeyDown);

    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = previousOverflow;
    };
  }, [open]);

  return (
    <>
      <ButtonLink
        href={site.macDownload}
        download
        size="lg"
        className="mt-7 w-full"
        onClick={() => setOpen(true)}
      >
        <DownloadIcon className="size-5" />
        Download Wallpache.dmg
      </ButtonLink>

      {open && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="mac-install-title"
          className="fixed inset-0 z-50 flex justify-center overflow-y-auto overscroll-contain bg-ink/80 p-4 backdrop-blur-sm sm:p-6"
          onClick={(event) => {
            if (event.target === event.currentTarget) setOpen(false);
          }}
        >
          <div className="glass glass-rim relative my-auto w-full max-w-xl rounded-3xl p-6 sm:p-8">
            <button
              ref={closeRef}
              type="button"
              onClick={() => setOpen(false)}
              aria-label="Close"
              className="absolute top-5 right-5 inline-flex size-9 items-center justify-center rounded-xl border border-white/10 bg-white/8 text-white/70 transition hover:bg-white/16 hover:text-white focus-visible:ring-2 focus-visible:ring-amber/70 focus-visible:outline-none"
            >
              <CloseIcon className="size-4" />
            </button>

            <p className="text-xs font-medium tracking-[0.14em] text-mint uppercase">
              Download started
            </p>
            <h2
              id="mac-install-title"
              className="font-heading mt-2 pr-12 text-2xl font-semibold tracking-tight text-balance"
            >
              Three steps, or macOS will call it{" "}
              <span className="text-gradient">“damaged”</span>
            </h2>
            <p className="mt-3 leading-relaxed text-pretty text-white/60">
              Nothing is wrong with your download. Wallpache is signed ad-hoc
              rather than with a paid Apple Developer ID, and macOS reports that
              as damage for anything that arrives through a browser. This clears
              it once, for good.
            </p>

            <ol className="mt-7 space-y-5">
              {steps.map((step, index) => (
                <li key={step.title} className="flex gap-4">
                  <span className="glass-soft mt-0.5 inline-flex size-7 shrink-0 items-center justify-center rounded-full text-sm font-semibold text-white/80">
                    {index + 1}
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="font-medium text-white/85">{step.title}</p>
                    <div className="mt-1 text-sm leading-relaxed text-white/50">
                      {step.body}
                    </div>
                  </div>
                </li>
              ))}
            </ol>

            <div className="mt-7 border-t border-white/10 pt-6">
              <p className="text-sm text-white/50">
                Rather do none of that? This one line downloads, installs and
                clears the flag for you:
              </p>
              <CopyCommand
                command={site.installCommand}
                display="curl -fsSL …/install.sh | bash"
                className="mt-3"
              />
            </div>
          </div>
        </div>
      )}
    </>
  );
}
