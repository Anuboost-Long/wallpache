import type { ComponentType, ReactNode, SVGProps } from "react";

import { ButtonLink } from "@/components/ui/ButtonLink";
import { CopyCommand } from "@/components/ui/CopyCommand";
import { AppleIcon, DownloadIcon, WindowsIcon } from "@/components/ui/icons";
import { MacDownloadButton } from "@/components/ui/MacDownloadButton";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { site } from "@/lib/site";

type PlatformCardProps = Readonly<{
  icon: ComponentType<SVGProps<SVGSVGElement>>;
  name: string;
  requirements: string;
  cta: string;
  href: string;
  children: ReactNode;
  note: ReactNode;
  /** Replaces the plain download button, for a platform that needs more than a link. */
  action?: ReactNode;
}>;

function PlatformCard({
  icon: Icon,
  name,
  requirements,
  cta,
  href,
  children,
  note,
  action,
}: PlatformCardProps) {
  return (
    <article className="glass glass-rim relative flex h-full flex-col rounded-3xl p-7 sm:p-8">
      <header className="flex items-center gap-4">
        <span className="glass-soft inline-flex size-13 items-center justify-center rounded-2xl">
          <Icon className="size-7 text-white/90" />
        </span>
        <div>
          <h3 className="font-heading text-2xl font-semibold tracking-tight">{name}</h3>
          <p className="text-sm text-white/50">{requirements}</p>
        </div>
      </header>

      {action ?? (
        <ButtonLink href={href} download size="lg" className="mt-7 w-full">
          <DownloadIcon className="size-5" />
          {cta}
        </ButtonLink>
      )}

      <div className="mt-6 flex-1">{children}</div>

      <p className="mt-6 text-sm leading-relaxed text-white/45">{note}</p>
    </article>
  );
}

const windowsHighlights = [
  "Loops behind your desktop icons, icons stay clickable",
  "Kept out of Alt+Tab and off the taskbar",
  "Tray controls, per-monitor wallpapers, launch at sign-in",
];

export function Download() {
  return (
    <section id="download" className="scroll-mt-28 px-4 py-24 sm:px-6">
      <div className="mx-auto max-w-6xl">
        <SectionHeading
          eyebrow="Free download"
          title={
            <>
              Pick your <span className="text-gradient">platform</span>
            </>
          }
          body="Both builds ship from the same public releases repo."
        />

        <div className="mt-14 grid gap-5 lg:grid-cols-2">
          <Reveal className="h-full">
            <PlatformCard
              icon={AppleIcon}
              name="macOS"
              requirements="Ventura 13.0 or later · Universal"
              cta="Download Wallpache.dmg"
              href={site.macDownload}
              action={<MacDownloadButton />}
              note={
                <>
                  The .dmg needs one Terminal command after it lands — the
                  download walks you through it. The one-line installer does that
                  step for you, which is why it&apos;s the route we recommend.
                </>
              }
            >
              <p className="mb-3 text-xs font-medium tracking-[0.14em] text-white/40 uppercase">
                or install from Terminal
              </p>
              <CopyCommand
                command={site.installCommand}
                display="curl -fsSL …/install.sh | bash"
              />
            </PlatformCard>
          </Reveal>

          <Reveal delay={110} className="h-full">
            <PlatformCard
              icon={WindowsIcon}
              name="Windows"
              requirements="Windows 10 (2004) & 11 · x64"
              cta="Download for Windows"
              href={site.windowsDownload}
              note="Pauses on sleep and lock, and puts itself back together if Explorer restarts."
            >
              <p className="mb-3 text-xs font-medium tracking-[0.14em] text-white/40 uppercase">
                what you get
              </p>
              <ul className="space-y-2.5">
                {windowsHighlights.map((item) => (
                  <li
                    key={item}
                    className="flex gap-3 text-sm leading-relaxed text-white/65"
                  >
                    <span className="mt-2 size-1.5 shrink-0 rounded-full bg-[linear-gradient(140deg,var(--color-sky),var(--color-violet))]" />
                    {item}
                  </li>
                ))}
              </ul>
            </PlatformCard>
          </Reveal>
        </div>

        <div className="mt-5 grid gap-5 lg:grid-cols-2">
          <Reveal className="h-full">
            <div className="glass glass-rim relative flex h-full flex-col rounded-3xl p-7 sm:p-8">
              <h3 className="font-heading text-lg font-semibold tracking-tight">
                ⚠️ macOS says the app is “damaged”?
              </h3>
              <p className="mt-2.5 leading-relaxed text-pretty text-white/60">
                Your download is fine. Wallpache is signed ad-hoc rather than with a
                paid Apple Developer ID certificate, and macOS reports that as damage
                for anything arriving through a browser. Clear the flag once:
              </p>
              <CopyCommand
                command={site.quarantineCommand}
                wrap
                className="mt-5"
              />
            </div>
          </Reveal>

          <Reveal delay={110} className="h-full">
            <div className="glass glass-rim relative flex h-full flex-col rounded-3xl p-7 sm:p-8">
              <h3 className="font-heading text-lg font-semibold tracking-tight">
                ⚠️ Windows says the publisher is unknown?
              </h3>
              <p className="mt-2.5 leading-relaxed text-pretty text-white/60">
                Your download is fine. Wallpache isn&apos;t yet signed with a paid
                code-signing certificate, so Windows has no publisher identity to
                vouch for and shows a SmartScreen warning. Click{" "}
                <span className="text-white/80">More info</span>, then{" "}
                <span className="text-white/80">Run anyway</span> — the same trust
                gap as the macOS warning, just Windows&apos; version of it.
              </p>
            </div>
          </Reveal>
        </div>
      </div>
    </section>
  );
}
