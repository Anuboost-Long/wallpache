"use client";

import { useEffect } from "react";

/**
 * Strips a lingering `#section` from the URL once the user scrolls back to
 * the top — clicking a nav link is supposed to be the only thing that sets
 * the hash, not something that stays stapled to the address bar forever.
 */
export function ScrollHashCleaner() {
  useEffect(() => {
    let ticking = false;

    const clearHashAtTop = () => {
      ticking = false;
      if (window.scrollY > 4 || !window.location.hash) return;
      history.replaceState(null, "", window.location.pathname + window.location.search);
    };

    const onScroll = () => {
      if (ticking) return;
      ticking = true;
      requestAnimationFrame(clearHashAtTop);
    };

    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  return null;
}
