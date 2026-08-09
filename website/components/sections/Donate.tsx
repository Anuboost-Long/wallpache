import Image from "next/image";

import { HeartIcon } from "@/components/ui/icons";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";

export function Donate() {
  return (
    <section id="donate" className="scroll-mt-28 px-4 py-24 sm:px-6">
      <div className="mx-auto max-w-4xl">
        <SectionHeading
          eyebrow="Say thanks"
          title={
            <>
              Keep it <span className="text-gradient">free for everyone.</span>
            </>
          }
          body="Wallpache costs nothing and always will. If it made your desktop a little more fun, a donation goes a long way — no pressure at all."
        />

        <Reveal className="mt-12">
          <div className="glass glass-rim relative overflow-hidden rounded-[32px] p-7 sm:p-10">
            <span
              aria-hidden
              className="pointer-events-none absolute -top-20 -left-16 size-64 rounded-full bg-pink/30 blur-[90px]"
            />
            <span
              aria-hidden
              className="pointer-events-none absolute -right-16 -bottom-20 size-64 rounded-full bg-amber/25 blur-[90px]"
            />

            <div className="relative flex flex-col items-center gap-8 sm:flex-row sm:items-center sm:gap-10">
              <div className="shrink-0 rounded-3xl bg-white p-3 shadow-[0_20px_50px_-20px_rgba(0,0,0,0.6)]">
                <Image
                  src="/donate-qr.jpg"
                  alt="KHQR donation code for Kimlong Ly"
                  width={903}
                  height={1270}
                  className="h-56 w-auto rounded-2xl sm:h-64"
                />
              </div>

              <div className="text-center sm:text-left">
                <span className="glass-soft inline-flex items-center gap-2 rounded-full px-3.5 py-1.5 text-xs font-medium text-white/75">
                  <HeartIcon className="size-3.5 text-pink" />
                  Made solo, kept free
                </span>

                <p className="mt-4 max-w-sm text-pretty text-white/65">
                  Scan with any KHQR-enabled banking app to send a tip to{" "}
                  <span className="font-medium text-white/85">Kimlong Ly</span>,
                  the person building Wallpache. Every bit helps keep it
                  maintained and ad-free.
                </p>

                <p className="mt-4 text-sm text-white/40">
                  Not able to scan? A screenshot works too — thank you either
                  way. 💛
                </p>
              </div>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}
