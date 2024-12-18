import { Navbar } from "./Navbar";
import { Footer } from "./Footer";

export function Layout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen flex flex-col bg-gray-200 dark:bg-gray-900 text-gray-900 dark:text-gray-100">
      <header className="bg-gray-300 dark:bg-gray-800 border-b border-gray-400 dark:border-gray-700">
        <Navbar />
      </header>
      <main className="flex-1 container mx-auto p-4 md:p-8 grid gap-6">
        {children}
      </main>
      <Footer />
    </div>
  );
}
