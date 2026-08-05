import Image from "next/image";

import { ButtonLink } from "@/components/ui/ButtonLink";
import { DownloadIcon, GitHubIcon } from "@/components/ui/icons";
import { Reveal } from "@/components/ui/Reveal";
import { site } from "@/lib/site";

export function FinalCta() {
  return (
    <section className="px-4 pt-8 pb-28 sm:px-6">
      <div className="mx-auto max-w-4xl">
        <Reveal>
          <div className="glass glass-rim relative overflow-hidden rounded-[32px] px-7 py-14 text-center sm:px-14">
            <span className="pointer-events-none absolute -top-24 left-1/2 size-72 -translate-x-1/2 rounded-full bg-pink/35 blur-[90px]" />

            <Image
              src="/icon-512.png"
              alt=""
              width={104}
              height={104}
              className="animate-bob relative mx-auto rounded-[26px] drop-shadow-2xl"
            />

            <h2 className="relative mt-7 text-4xl font-semibold tracking-tight text-balance sm:text-5xl">
              Go on — make the desktop{" "}
              <span className="text-gradient">fun again.</span>
            </h2>
            <p className="relative mx-auto mt-4 max-w-md text-lg text-pretty text-white/60">
              Free, offline, and about as heavy as a screensaver from 1998.
            </p>

            <div className="relative mt-9 flex flex-wrap items-center justify-center gap-3">
              <ButtonLink href={site.latestRelease} size="lg">
                <DownloadIcon className="size-5" />
                Download the latest release
              </ButtonLink>
              <ButtonLink href={site.distRepo} variant="glass" size="lg">
                <GitHubIcon className="size-4.5" />
                Browse the dist repo
              </ButtonLink>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}
