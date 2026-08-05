"use client";

import clsx from "clsx";
import { useEffect, useRef, type CSSProperties, type ReactNode } from "react";

type RevealProps = Readonly<{
  children: ReactNode;
  /** Stagger within a group, in milliseconds. */
  delay?: number;
  className?: string;
}>;

/** Fades its content up the first time it scrolls into view, then stops watching. */
export function Reveal({ children, delay = 0, className }: RevealProps) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const node = ref.current;
    if (!node) return;

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (!entry.isIntersecting) return;
        entry.target.setAttribute("data-shown", "true");
        observer.disconnect();
      },
      { rootMargin: "0px 0px -12% 0px", threshold: 0.1 },
    );

    observer.observe(node);
    return () => observer.disconnect();
  }, []);

  return (
    <div
      ref={ref}
      // min-w-0: as a grid/flex child the wrapper must never impose the
      // automatic minimum size, or nowrap content inside widens the track.
      className={clsx("reveal min-w-0", className)}
      style={{ "--reveal-delay": `${delay}ms` } as CSSProperties}
    >
      {children}
    </div>
  );
}
