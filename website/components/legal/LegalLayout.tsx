import type { ReactNode } from "react";

type LegalLayoutProps = Readonly<{
  title: string;
  updated: string;
  children: ReactNode;
}>;

/** Shared shell for /terms and /privacy — plain, readable prose over the site's dark theme. */
export function LegalLayout({ title, updated, children }: LegalLayoutProps) {
  return (
    <section className="px-4 pt-36 pb-24 sm:px-6 sm:pt-44">
      <div className="mx-auto max-w-3xl">
        <h1 className="font-heading text-4xl font-semibold tracking-tight text-balance sm:text-5xl">
          {title}
        </h1>
        <p className="mt-3 text-sm text-white/45">Last updated: {updated}</p>

        <div
          className={[
            "mt-10 space-y-6 text-[0.975rem] leading-relaxed text-white/70",
            "[&_h2]:font-heading [&_h2]:mt-10 [&_h2]:text-xl [&_h2]:font-semibold [&_h2]:tracking-tight [&_h2]:text-white",
            "[&_h2:first-child]:mt-0",
            "[&_p]:leading-relaxed",
            "[&_ul]:list-disc [&_ul]:space-y-2 [&_ul]:pl-5",
            "[&_a]:text-amber [&_a]:underline [&_a]:decoration-amber/40 [&_a]:underline-offset-4 hover:[&_a]:decoration-amber",
            "[&_strong]:font-semibold [&_strong]:text-white/85",
          ].join(" ")}
        >
          {children}
        </div>
      </div>
    </section>
  );
}
