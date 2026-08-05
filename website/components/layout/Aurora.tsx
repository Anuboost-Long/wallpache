import clsx from "clsx";

/** One drifting colour blob. */
type BlobProps = Readonly<{ className: string }>;

function Blob({ className }: BlobProps) {
  return <span className={clsx("absolute rounded-full", className)} />;
}

/**
 * The page's colour field: the app icon's sunset blown up into slow-drifting
 * blobs behind everything, so the glass panels have something to refract.
 */
export function Aurora() {
  return (
    <div
      aria-hidden
      className="pointer-events-none fixed inset-0 -z-10 overflow-hidden bg-ink"
    >
      <Blob className="animate-drift -top-[20%] -left-[10%] size-[44rem] bg-grape/60 blur-[110px]" />
      <Blob className="animate-drift-slow -top-[6%] -right-[12%] size-[40rem] bg-pink/55 blur-[120px]" />
      <Blob className="animate-drift top-[30%] left-[24%] size-[36rem] bg-orange/45 blur-[130px] [animation-delay:-8s]" />
      <Blob className="animate-drift-slow top-[55%] -right-[8%] size-[40rem] bg-amber/40 blur-[140px] [animation-delay:-14s]" />
      <Blob className="animate-drift bottom-[8%] -left-[14%] size-[38rem] bg-violet/55 blur-[120px] [animation-delay:-20s]" />
      <Blob className="animate-drift-slow -bottom-[18%] left-[38%] size-[34rem] bg-sky/30 blur-[140px] [animation-delay:-26s]" />

      {/* Vignette keeps type readable without flattening the colour */}
      <div className="absolute inset-0 bg-[radial-gradient(130%_90%_at_50%_10%,transparent_35%,rgba(11,7,24,0.6)_100%)]" />

      {/* Fine noise — stops the gradients from banding */}
      <div
        className="absolute inset-0 opacity-[0.15] mix-blend-overlay"
        style={{
          backgroundImage:
            "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='160' height='160'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='3'/%3E%3C/filter%3E%3Crect width='160' height='160' filter='url(%23n)' opacity='0.5'/%3E%3C/svg%3E\")",
        }}
      />
    </div>
  );
}
