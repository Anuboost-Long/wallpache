import type { Metadata } from "next";
import Link from "next/link";

import { LegalLayout } from "@/components/legal/LegalLayout";
import { site } from "@/lib/site";

export const metadata: Metadata = {
  title: `Terms of Service — ${site.name}`,
  description: `The terms that govern downloading and using ${site.name}.`,
};

const UPDATED = "August 9, 2026";

export default function TermsPage() {
  return (
    <LegalLayout title="Terms of Service" updated={UPDATED}>
      <p>
        These Terms of Service (&ldquo;Terms&rdquo;) govern your access to and use of{" "}
        {site.name} (the &ldquo;Software&rdquo;) and this website (together, the
        &ldquo;Service&rdquo;), provided by {site.copyrightHolder} (&ldquo;{site.name}
        ,&rdquo; &ldquo;we,&rdquo; &ldquo;us,&rdquo; or &ldquo;our&rdquo;). By
        downloading, installing, or using the Software, or by using this website,
        you agree to be bound by these Terms. If you do not agree, do not download,
        install, or use the Software.
      </p>

      <h2>1. The Service</h2>
      <p>
        {site.name} is a free desktop application for macOS and Windows that loops a
        video file behind your desktop icons. It does not require an account, does
        not phone home, and does not collect or transmit your files. See our{" "}
        <Link href="/privacy">Privacy Policy</Link> for details.
      </p>

      <h2>2. Ownership and intellectual property</h2>
      <p>
        The Software, including its source code, object code, design, user
        interface, logos, icons, and the {site.name} name and mark, is the
        exclusive property of {site.copyrightHolder} and is protected by
        copyright, trademark, and other intellectual property laws.{" "}
        <strong>{site.name} is not open source.</strong> Publishing installers or
        release binaries in a public repository does not grant any license to the
        underlying source code, and no rights in the Software are transferred to
        you except the limited license expressly granted in Section 3.
      </p>
      <p>
        Third-party components distributed with the Software remain the property
        of their respective owners and are used under their own license terms.
      </p>

      <h2>3. License to use the Software</h2>
      <p>
        Subject to your compliance with these Terms, {site.copyrightHolder} grants
        you a personal, non-exclusive, non-transferable, revocable, royalty-free
        license to download, install, and run the Software on devices you own or
        control, for your own personal or internal use.
      </p>
      <p>You may not, and may not permit anyone else to:</p>
      <ul>
        <li>
          copy, modify, reverse engineer, decompile, or disassemble the Software,
          except to the extent applicable law expressly permits this despite the
          restriction;
        </li>
        <li>
          redistribute, resell, sublicense, rent, lease, or otherwise make the
          Software available to third parties as if it were your own or a
          competing product;
        </li>
        <li>
          remove, obscure, or alter any copyright, trademark, or other proprietary
          notice included in or displayed by the Software or this website;
        </li>
        <li>
          use the {site.name} name, logo, or branding to imply endorsement of, or
          affiliation with, any other product or service without our prior written
          consent; or
        </li>
        <li>
          claim authorship or ownership of the Software or any part of it.
        </li>
      </ul>

      <h2>4. Your content</h2>
      <p>
        Any video or image files you import into the Software remain entirely
        yours. We claim no ownership over your content, and — as described in the
        Privacy Policy — that content never leaves your device through the
        Software.
      </p>

      <h2>5. No warranty</h2>
      <p>
        THE SOFTWARE IS PROVIDED &ldquo;AS IS&rdquo; AND &ldquo;AS AVAILABLE,&rdquo;
        WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
        TO WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE,
        NON-INFRINGEMENT, OR THAT THE SOFTWARE WILL BE UNINTERRUPTED, ERROR-FREE,
        OR SECURE. YOU USE THE SOFTWARE AT YOUR OWN RISK.
      </p>

      <h2>6. Limitation of liability</h2>
      <p>
        TO THE MAXIMUM EXTENT PERMITTED BY LAW, IN NO EVENT WILL{" "}
        {site.copyrightHolder} BE LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL,
        CONSEQUENTIAL, OR PUNITIVE DAMAGES, OR ANY LOSS OF DATA, PROFITS, OR
        GOODWILL, ARISING FROM OR RELATED TO YOUR USE OF, OR INABILITY TO USE, THE
        SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGES. TO THE
        MAXIMUM EXTENT PERMITTED BY LAW, OUR TOTAL LIABILITY FOR ANY CLAIM ARISING
        OUT OF OR RELATING TO THE SOFTWARE OR THESE TERMS WILL NOT EXCEED THE
        AMOUNT YOU PAID FOR THE SOFTWARE, WHICH — AS THE SOFTWARE IS PROVIDED FREE
        OF CHARGE — IS ZERO.
      </p>

      <h2>7. Indemnification</h2>
      <p>
        You agree to defend, indemnify, and hold harmless {site.copyrightHolder}{" "}
        from any claim, liability, damage, loss, or expense (including reasonable
        legal fees) arising out of your use of the Software, your content, or your
        violation of these Terms.
      </p>

      <h2>8. Termination</h2>
      <p>
        This license is effective until terminated. It terminates automatically,
        without notice, if you fail to comply with any provision of these Terms.
        Upon termination, you must stop using the Software and delete all copies
        of it in your possession.
      </p>

      <h2>9. Changes to these Terms</h2>
      <p>
        We may update these Terms from time to time. Material changes will be
        reflected by updating the &ldquo;Last updated&rdquo; date above. Continued
        use of the Software or this website after changes take effect constitutes
        acceptance of the revised Terms.
      </p>

      <h2>10. Governing law</h2>
      <p>
        These Terms are governed by the laws of the United States, without regard
        to its conflict-of-law principles, to the extent applicable to your use of
        the Service.
      </p>

      <h2>11. Contact</h2>
      <p>
        Questions about these Terms can be sent to{" "}
        <a href={`mailto:${site.legalEmail}`}>{site.legalEmail}</a>.
      </p>
    </LegalLayout>
  );
}
