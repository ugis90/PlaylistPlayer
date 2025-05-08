// src/components/Layout.tsx - Standardized light mode

import React, { ReactNode } from "react";
import Navigation from "./Navigation";

interface LayoutProps {
  children: ReactNode;
}

const Layout: React.FC<LayoutProps> = ({ children }) => {
  return (
    <div className="min-h-screen flex flex-col bg-gray-100">
      <header className="bg-white border-b border-gray-200">
        <Navigation />
      </header>
      <main className="flex-1 container mx-auto p-4 md:p-8 grid gap-6">
        {children}
      </main>
      <footer className="bg-gray-800 text-white py-4 text-center">
        <p className="text-sm">
          &copy; {new Date().getFullYear()} Family Fleet Management
        </p>
      </footer>
    </div>
  );
};

export default Layout;
