import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { steps } from "@/lib/content";

export function HowItWorks() {
  return (
    <section id="how" className="scroll-mt-28 px-4 py-24 sm:px-6">
      <div className="mx-auto max-w-6xl">
        <SectionHeading
          eyebrow="Four steps"
          title={
            <>
              From download to a{" "}
              <span className="text-gradient">moving desktop</span>
            </>
          }
          body="No account, no sign-in, no cloud. It's a menu-bar app that plays your own files."
        />

        <ol className="mt-14 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
          {steps.map((step, index) => (
            <Reveal key={step.title} delay={index * 90} className="h-full">
              <li className="glass glass-rim relative h-full rounded-3xl p-7 pt-9">
                <span className="absolute -top-4 left-7 inline-flex size-9 items-center justify-center rounded-xl bg-[linear-gradient(140deg,var(--color-amber),var(--color-pink))] text-sm font-bold text-ink shadow-lg">
                  {index + 1}
                </span>
                <h3 className="text-lg font-semibold tracking-tight">
                  {step.title}
                </h3>
                <p className="mt-2 text-sm leading-relaxed text-pretty text-white/60">
                  {step.body}
                </p>
              </li>
            </Reveal>
          ))}
        </ol>
      </div>
    </section>
  );
}
