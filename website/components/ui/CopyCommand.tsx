"use client";

import clsx from "clsx";
import { useEffect, useState } from "react";

import { CheckIcon, CopyIcon } from "@/components/ui/icons";

type CopyCommandProps = Readonly<{
  /** The command actually copied to the clipboard. */
  command: string;
  /** Shorter text to display, when the real command is too long for the box. */
  display?: string;
  label?: string;
  className?: string;
  /** Wraps onto a second line instead of truncating, where the command itself is the point. */
  wrap?: boolean;
}>;

export function CopyCommand({
  command,
  display,
  label,
  className,
  wrap = false,
}: CopyCommandProps) {
  const [copied, setCopied] = useState(false);

  // Let the "Copied" state fall back on its own rather than leaving it stuck.
  useEffect(() => {
    if (!copied) return;
    const timer = setTimeout(() => setCopied(false), 2000);
    return () => clearTimeout(timer);
  }, [copied]);

  async function copy() {
    try {
      await navigator.clipboard.writeText(command);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  }

  return (
    <div
      className={clsx(
        "glass glass-rim relative flex items-center gap-3 rounded-2xl p-2 pl-4",
        "font-mono text-sm",
        className,
      )}
    >
      {label && (
        <span className="hidden shrink-0 text-xs tracking-wide text-amber/80 uppercase sm:block">
          {label}
        </span>
      )}

      <code
        className={clsx(
          "min-w-0 flex-1 text-white/75",
          wrap ? "py-1 [overflow-wrap:break-word]" : "truncate",
        )}
      >
        <span className="mr-2 text-mint">$</span>
        {display ?? command}
      </code>

      <button
        type="button"
        onClick={copy}
        aria-label={copied ? "Copied" : "Copy command"}
        className={clsx(
          "inline-flex h-9 shrink-0 items-center gap-1.5 rounded-xl px-3",
          "bg-white/8 hover:bg-white/16",
          "border border-white/10",
          "text-xs font-medium text-white/80 hover:text-white",
          "transition",
          "focus-visible:ring-2 focus-visible:ring-amber/70 focus-visible:outline-none",
        )}
      >
        {copied ? (
          <CheckIcon className="size-4 text-mint" />
        ) : (
          <CopyIcon className="size-4" />
        )}
        <span className="hidden sm:inline">{copied ? "Copied" : "Copy"}</span>
      </button>
    </div>
  );
}
