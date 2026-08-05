"use client";

import { useEffect, useRef } from "react";

/**
 * The real thing: a screen recording of the library window applying a wallpaper,
 * looping inside a glass frame. Muted and inert — it is decoration, not a player.
 */
export function DesktopPreview() {
  const ref = useRef<HTMLVideoElement>(null);

  useEffect(() => {
    const video = ref.current;
    if (!video) return;

    // Autoplay is a motion effect like any other; hold the poster frame
    // instead when the visitor has asked for less of it.
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      video.pause();
      return;
    }

    // Drive playback off visibility rather than trusting the autoplay attribute
    // alone: browsers pause muted autoplay while the element is off-screen and
    // do not reliably resume. Pausing when scrolled away is the right behaviour
    // for a decorative loop anyway — the app itself makes the same argument.
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          video.play().catch(() => {
            // An autoplay refusal just leaves the poster up — nothing to recover.
          });
        } else {
          video.pause();
        }
      },
      { threshold: 0.2 },
    );

    observer.observe(video);
    return () => observer.disconnect();
  }, []);

  return (
    <div className="relative">
      <div className="glass glass-rim relative overflow-hidden rounded-[26px] p-2.5 shadow-[0_50px_120px_-40px_rgba(168,85,247,0.55)]">
        <video
          ref={ref}
          className="block w-full rounded-[18px]"
          src="/wallpache-demo.mp4"
          poster="/wallpache-demo-poster.jpg"
          width={1280}
          height={832}
          autoPlay
          muted
          loop
          playsInline
          preload="metadata"
          aria-label="Wallpache applying a video wallpaper to the desktop"
        />
      </div>

      {/* Status pills — the app narrating itself */}
      <div
        aria-hidden
        className="glass animate-bob absolute -top-5 -left-4 rounded-full px-4 py-2 text-xs font-medium text-white/85 sm:-left-8"
      >
        🔋 Paused in Low Power Mode
      </div>
      <div
        aria-hidden
        className="glass animate-bob absolute -right-3 -bottom-6 rounded-full px-4 py-2 text-xs font-medium text-white/85 [animation-delay:-3.5s] sm:-right-8"
      >
        🖥 Playing on 2 displays
      </div>
    </div>
  );
}
