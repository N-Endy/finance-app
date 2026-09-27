import type { NextConfig } from "next";

const apiOrigin = process.env.FINANCEOS_API_ORIGIN ?? "http://localhost:5080";

const nextConfig: NextConfig = {
  output: "standalone",
  async rewrites() {
    return [
      {
        source: "/api/v1/:path*",
        destination: `${apiOrigin}/api/v1/:path*`
      }
    ];
  }
};

export default nextConfig;
