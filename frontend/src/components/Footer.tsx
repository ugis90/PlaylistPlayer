export function Footer() {
  return (
    <footer className="bg-gray-900 text-gray-100 py-8">
      <div className="container mx-auto px-4">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          <div>
            <h3 className="text-xl font-bold mb-4">PlaylistPlayer</h3>
            <p className="text-gray-400">
              Manage your music collections with ease
            </p>
          </div>
          <div>
            <h4 className="text-lg font-bold mb-4">Quick Links</h4>
            <nav className="space-y-2">
              <a href="/" className="block text-gray-400 hover:text-white">
                Home
              </a>
              <a href="#" className="block text-gray-400 hover:text-white">
                About
              </a>
            </nav>
          </div>
          <div>
            <h4 className="text-lg font-bold mb-4">Connect</h4>
            <div className="flex space-x-4">{/* Add social icons */}</div>
          </div>
        </div>
      </div>
    </footer>
  );
}
