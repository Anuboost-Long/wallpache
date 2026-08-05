import type { ComponentType, ReactNode, SVGProps } from "react";

import { ButtonLink } from "@/components/ui/ButtonLink";
import { CopyCommand } from "@/components/ui/CopyCommand";
import { AppleIcon, DownloadIcon, WindowsIcon } from "@/components/ui/icons";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { site } from "@/lib/site";

type PlatformCardProps = Readonly<{
  icon: ComponentType<SVGProps<SVGSVGElement>>;
  name: string;
  requirements: string;
  cta: string;
  children: ReactNode;
  note: ReactNode;
}>;

function PlatformCard({
  icon: Icon,
  name,
  requirements,
  cta,
  children,
  note,
}: PlatformCardProps) {
  return (
    <article className="glass glass-rim relative flex h-full flex-col rounded-3xl p-7 sm:p-8">
      <header className="flex items-center gap-4">
        <span className="glass-soft inline-flex size-13 items-center justify-center rounded-2xl">
          <Icon className="size-7 text-white/90" />
        </span>
        <div>
          <h3 className="text-2xl font-semibold tracking-tight">{name}</h3>
          <p className="text-sm text-white/50">{requirements}</p>
        </div>
      </header>

      <ButtonLink href={site.latestRelease} size="lg" className="mt-7 w-full">
        <DownloadIcon className="size-5" />
        {cta}
      </ButtonLink>

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
              note={
                <>
                  The one-line installer clears the quarantine flag for you —
                  which is exactly why it&apos;s the route we recommend.
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

        <Reveal className="mt-5">
          <div className="glass glass-rim relative rounded-3xl p-7 sm:p-8">
            <h3 className="text-lg font-semibold tracking-tight">
              ⚠️ macOS says the app is “damaged”?
            </h3>
            <p className="mt-2.5 max-w-3xl leading-relaxed text-pretty text-white/60">
              Your download is fine. Wallpache is signed ad-hoc rather than with a
              paid Apple Developer ID certificate, and macOS reports that as damage
              for anything arriving through a browser. Clear the flag once:
            </p>
            <CopyCommand
              command={site.quarantineCommand}
              className="mt-5 max-w-2xl"
            />
          </div>
        </Reveal>
      </div>
    </section>
  );
}
