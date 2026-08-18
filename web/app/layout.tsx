import type { Metadata } from "next";
import { Toaster } from "@shared/ui/toast";
import "./globals.css";

export const metadata: Metadata = {
  title: "JobTracker",
  description: "Roofing job management dashboard",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className="min-h-screen bg-gray-50 font-sans antialiased" suppressHydrationWarning>
        {children}
        <Toaster />
      </body>
    </html>
  );
}
