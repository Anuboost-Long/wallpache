import clsx from "clsx";

import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { features, type Feature } from "@/lib/content";

type FeatureCardProps = Readonly<{ feature: Feature }>;

function FeatureCard({ feature }: FeatureCardProps) {
  const { icon: Icon, from, to } = feature;

  return (
    <article
      className={clsx(
        "glass glass-rim group relative h-full overflow-hidden rounded-3xl p-7",
        "transition duration-500 hover:-translate-y-1.5 hover:border-white/22",
      )}
    >
      {/* Bloom in the card's own colour, lit on hover */}
      <span
        className="pointer-events-none absolute -top-16 -right-12 size-40 rounded-full opacity-25 blur-3xl transition duration-500 group-hover:opacity-55"
        style={{ backgroundImage: `linear-gradient(140deg, ${from}, ${to})` }}
      />

      <span
        className="relative inline-flex size-12 items-center justify-center rounded-2xl text-ink shadow-lg"
        style={{ backgroundImage: `linear-gradient(140deg, ${from}, ${to})` }}
      >
        <Icon className="size-6" />
      </span>

      <h3 className="font-heading relative mt-5 text-xl font-semibold tracking-tight">
        {feature.title}
      </h3>
      <p className="relative mt-2.5 leading-relaxed text-pretty text-white/60">
        {feature.body}
      </p>
    </article>
  );
}

export function Features() {
  return (
    <section id="features" className="scroll-mt-28 px-4 py-24 sm:px-6">
      <div className="mx-auto max-w-6xl">
        <SectionHeading
          eyebrow="The good stuff"
          title={
            <>
              Small app. <span className="text-gradient">Strong opinions.</span>
            </>
          }
          body="Wallpache does one thing, and refuses to charge you battery, clicks, or attention for it."
        />

        <div className="mt-14 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {features.map((feature, index) => (
            <Reveal key={feature.title} delay={index * 70}>
              <FeatureCard feature={feature} />
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}
