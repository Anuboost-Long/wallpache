import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Static HTML in `out/`, so the landing page can be hosted anywhere —
  // GitHub Pages included. Image optimisation needs a server, hence unoptimized.
  output: "export",
  images: { unoptimized: true },
  trailingSlash: true,
};

export default nextConfig;
