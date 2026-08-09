import type { Metadata } from "next";

import { LegalLayout } from "@/components/legal/LegalLayout";
import { site } from "@/lib/site";

export const metadata: Metadata = {
  title: `Privacy Policy — ${site.name}`,
  description: `What ${site.name} does — and does not — collect.`,
};

const UPDATED = "August 9, 2026";

export default function PrivacyPage() {
  return (
    <LegalLayout title="Privacy Policy" updated={UPDATED}>
      <p>
        This policy explains what {site.name} (&ldquo;we,&rdquo; &ldquo;us,&rdquo;
        or &ldquo;our&rdquo;) does with your data. The short version: the app
        doesn&rsquo;t collect any.
      </p>

      <h2>1. The Software collects nothing</h2>
      <p>{site.name} the desktop application:</p>
      <ul>
        <li>does not require an account, sign-in, or registration;</li>
        <li>does not collect filenames, file contents, or metadata about your videos;</li>
        <li>does not include analytics, telemetry, or crash reporting;</li>
        <li>
          makes no outbound network requests during import, playback, or idle use;
        </li>
        <li>
          stores your imported files only on your own device, in a location the app
          shows you and lets you clear.
        </li>
      </ul>
      <p>
        Because nothing is transmitted, we have no server logs, analytics
        dashboard, or database of app usage to describe — there simply isn&rsquo;t
        one.
      </p>

      <h2>2. This website</h2>
      <p>
        This website is a static site with no accounts, forms, or first-party
        analytics or advertising scripts. It is hosted by a third-party static
        hosting provider, which may record standard web server logs (such as IP
        address, browser type, and requested page) for operating and securing the
        hosting infrastructure. We do not access these logs for tracking purposes
        and do not combine them with any other data.
      </p>

      <h2>3. Third-party services</h2>
      <p>
        Downloads and release files are distributed via {site.distRepo}, a
        third-party code hosting platform. Your use of that site is subject to
        its own privacy policy and terms, which we don&rsquo;t control.
      </p>

      <h2>4. Children&rsquo;s privacy</h2>
      <p>
        The Service is not directed at children, and since we don&rsquo;t collect
        personal information from anyone, we don&rsquo;t knowingly collect it from
        children either.
      </p>

      <h2>5. Changes to this policy</h2>
      <p>
        If what the Software or website collects ever changes, we&rsquo;ll update
        this page and the &ldquo;Last updated&rdquo; date above before that change
        ships.
      </p>

      <h2>6. Contact</h2>
      <p>
        Questions about this policy can be sent to{" "}
        <a href={`mailto:${site.legalEmail}`}>{site.legalEmail}</a>.
      </p>
    </LegalLayout>
  );
}
