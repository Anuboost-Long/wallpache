import { DesktopPreview } from "@/components/sections/DesktopPreview";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { CopyCommand } from "@/components/ui/CopyCommand";
import { DownloadIcon } from "@/components/ui/icons";
import { Reveal } from "@/components/ui/Reveal";
import { site } from "@/lib/site";

const badges = [
  "Universal — Apple Silicon + Intel",
  "Lives in the menu bar & tray",
  "Nothing ever uploaded",
];

export function Hero() {
  return (
    <section className="px-4 pt-36 pb-20 sm:px-6 sm:pt-44 lg:pb-28">
      <div className="mx-auto grid max-w-6xl items-center gap-16 lg:grid-cols-[1.05fr_1fr] lg:gap-12">
        <Reveal>
          <span className="glass-soft inline-flex items-center gap-2.5 rounded-full py-2 pr-4 pl-3 text-xs font-medium text-white/75">
            <span className="animate-glow size-2 rounded-full bg-mint" />
            macOS &amp; Windows · free · no account
          </span>

          <h1 className="mt-6 text-5xl leading-[1.02] font-semibold tracking-tight text-balance sm:text-6xl lg:text-7xl">
            Your wallpaper{" "}
            <span className="text-gradient">should move.</span>
          </h1>

          <p className="mt-6 max-w-xl text-lg leading-relaxed text-pretty text-white/65">
            {site.description}
          </p>

          <div className="mt-9 flex flex-wrap items-center gap-3">
            <ButtonLink href="#download" size="lg">
              <DownloadIcon className="size-5" />
              Download free
            </ButtonLink>
            <ButtonLink href="#how" variant="glass" size="lg">
              See how it works
            </ButtonLink>
          </div>

          <CopyCommand
            command={site.installCommand}
            display="curl -fsSL …/wallpache-dist/main/install.sh | bash"
            label="macOS"
            className="mt-6 max-w-xl"
          />

          <ul className="mt-8 flex flex-wrap gap-x-6 gap-y-2 text-sm text-white/45">
            {badges.map((badge) => (
              <li key={badge} className="flex items-center gap-2">
                <span className="size-1 rounded-full bg-amber" />
                {badge}
              </li>
            ))}
          </ul>
        </Reveal>

        <Reveal delay={150} className="lg:pl-4">
          <DesktopPreview />
        </Reveal>
      </div>
    </section>
  );
}
