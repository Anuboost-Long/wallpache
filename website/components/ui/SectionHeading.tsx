import clsx from "clsx";
import type { ReactNode } from "react";

import { Reveal } from "@/components/ui/Reveal";

type SectionHeadingProps = Readonly<{
  eyebrow: string;
  title: ReactNode;
  body?: string;
  className?: string;
}>;

export function SectionHeading({
  eyebrow,
  title,
  body,
  className,
}: SectionHeadingProps) {
  return (
    <Reveal className={clsx("mx-auto max-w-2xl text-center", className)}>
      <span
        className={clsx(
          "glass-soft inline-flex items-center rounded-full px-3.5 py-1.5",
          "text-xs font-medium tracking-[0.14em] text-amber uppercase",
        )}
      >
        {eyebrow}
      </span>

      <h2 className="font-heading mt-5 text-4xl font-semibold tracking-tight text-balance sm:text-5xl">
        {title}
      </h2>

      {body && (
        <p className="mt-4 text-lg leading-relaxed text-pretty text-white/60">
          {body}
        </p>
      )}
    </Reveal>
  );
}
