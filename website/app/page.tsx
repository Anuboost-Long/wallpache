import { Download } from "@/components/sections/Download";
import { Features } from "@/components/sections/Features";
import { FinalCta } from "@/components/sections/FinalCta";
import { Hero } from "@/components/sections/Hero";
import { HowItWorks } from "@/components/sections/HowItWorks";

export default function Home() {
  return (
    <>
      <Hero />
      <Features />
      <HowItWorks />
      <Download />
      <FinalCta />
    </>
  );
}
