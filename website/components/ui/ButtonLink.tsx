import clsx from "clsx";
import Link from "next/link";
import type { ReactNode } from "react";

type Variant = "solid" | "glass" | "ghost";
type Size = "sm" | "md" | "lg";

type ButtonLinkProps = Readonly<{
  href: string;
  children: ReactNode;
  variant?: Variant;
  size?: Size;
  className?: string;
  /**
   * File downloads (release assets, etc). Skips `target="_blank"` so the
   * browser just downloads in place instead of opening — and leaving behind
   * — a blank tab on github.com.
   */
  download?: boolean;
}>;

const variantClasses: Record<Variant, string> = {
  solid: clsx(
    "bg-[linear-gradient(100deg,var(--color-amber),var(--color-orange)_45%,var(--color-pink))] bg-[length:200%_auto] hover:bg-[position:100%_50%]",
    "text-ink font-semibold",
    "shadow-[0_10px_34px_-12px_rgba(255,78,142,0.8)] hover:shadow-[0_18px_44px_-12px_rgba(255,122,69,0.95)]",
    "hover:-translate-y-0.5",
  ),
  glass: clsx(
    "glass",
    "border-white/12 hover:border-white/25",
    "text-white/85 hover:text-white",
    "hover:-translate-y-0.5",
  ),
  ghost: clsx(
    "border border-transparent hover:border-white/12 hover:bg-white/8",
    "text-white/70 hover:text-white",
  ),
};

const sizeClasses: Record<Size, string> = {
  sm: "h-9 gap-1.5 px-4 text-sm",
  md: "h-11 gap-2 px-5 text-[0.95rem]",
  lg: "h-13 gap-2.5 px-7 text-base",
};

export function ButtonLink({
  href,
  children,
  variant = "solid",
  size = "md",
  className,
  download = false,
}: ButtonLinkProps) {
  const isExternal = href.startsWith("http");

  return (
    <Link
      href={href}
      className={clsx(
        "inline-flex items-center justify-center rounded-full whitespace-nowrap",
        "transition duration-300 outline-none",
        "focus-visible:ring-2 focus-visible:ring-amber/70 focus-visible:ring-offset-2 focus-visible:ring-offset-ink",
        variantClasses[variant],
        sizeClasses[size],
        className,
      )}
      {...(download
        ? { download: true }
        : isExternal && { target: "_blank", rel: "noopener noreferrer" })}
    >
      {children}
    </Link>
  );
}
