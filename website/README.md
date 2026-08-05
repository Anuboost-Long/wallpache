# Wallpache — landing page

Marketing site for the macOS and Windows apps. Next.js 16 (App Router) +
Tailwind CSS v4, exported as static HTML so it can be hosted anywhere.

```bash
npm install
npm run dev      # http://localhost:3000
npm run build    # static site in out/
npm run lint
```

## Layout

```
app/
  layout.tsx      metadata, fonts, and the page shell (Aurora + header + footer)
  page.tsx        section order, nothing else
  globals.css     palette tokens, glass primitives, keyframes
components/
  layout/         Aurora, SiteHeader, SiteFooter
  sections/       Hero, DesktopPreview, Features, HowItWorks, Download, FinalCta
  ui/             ButtonLink, CopyCommand, Reveal, SectionHeading, icons
lib/
  site.ts         every outward-facing link and the install commands
  content.ts      feature and step copy
public/           wave mark, app icon, and the hero demo clip
```

Sections are data-driven: edit `lib/content.ts` for feature and step copy, and
`lib/site.ts` for download links. Only `SiteHeader`, `Reveal`, and `CopyCommand`
are client components — everything else renders on the server.

## The hero clip

`public/wallpache-demo.mp4` is a cut from a screen recording of the macOS app —
the library window applying a wallpaper, the desktop changing behind it, the
window closing. It is trimmed to 13.0s–20.0s of the source, scaled to 1280×832,
re-encoded video-only at 1.8 Mbps / 30 fps (142 MB → 1.5 MB), with
`wallpache-demo-poster.jpg` as its first frame.

`DesktopPreview` plays it from an `IntersectionObserver` rather than relying on
the `autoplay` attribute: browsers pause muted autoplay off-screen and do not
reliably resume. It stays paused entirely under `prefers-reduced-motion`.

To swap in a new recording, re-cut with the same shape: trim, scale to 1280
wide, drop the audio track, and target ~1.5 MB.

## Design

The palette is lifted from the app icon's sunset (`--color-violet`, `--color-pink`,
`--color-orange`, `--color-amber`, `--color-sun` over `--color-ink`). `Aurora`
paints drifting blobs of those colours behind the whole page, and the `.glass`
utilities in `globals.css` frost panels on top of them. Dark theme only — the
page commits to one look.

## Deploying

`next.config.ts` sets `output: "export"`, so `npm run build` writes plain HTML,
CSS and JS to `out/`. Upload that directory to GitHub Pages, Netlify, S3, or any
static host.

Set `NEXT_PUBLIC_SITE_URL` at build time to the final origin, so Open Graph image
URLs resolve absolutely:

```bash
NEXT_PUBLIC_SITE_URL="https://wallpache.example" npm run build
```
