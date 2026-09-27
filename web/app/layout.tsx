import type { Metadata, Viewport } from "next";
import { Outfit, Syne } from "next/font/google";
import { PwaRegister } from "@/components/pwa";
import "./globals.css";

const outfit = Outfit({ subsets: ["latin"], variable: "--font-body" });
const syne = Syne({ subsets: ["latin"], variable: "--font-display" });

export const metadata: Metadata = {
  title: "Finance OS",
  description: "Personal financial operating system. Every naira has a job.",
  applicationName: "Finance OS",
  appleWebApp: {
    capable: true,
    title: "Finance OS",
    statusBarStyle: "black-translucent"
  },
  icons: {
    icon: "/icons/icon-192.png",
    apple: "/icons/icon-192.png"
  }
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  viewportFit: "cover",
  themeColor: "#07110d"
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${outfit.variable} ${syne.variable}`}>
      <body className={outfit.className}>
        <PwaRegister />
        {children}
      </body>
    </html>
  );
}
