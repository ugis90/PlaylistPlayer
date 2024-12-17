// src/components/Breadcrumb.tsx
import { Link, useLocation } from "react-router-dom";

export function Breadcrumb() {
  const location = useLocation();
  const paths = location.pathname.split("/").filter(Boolean);

  return (
    <nav className="mb-4">
      <ol className="flex gap-2 text-sm">
        <li key="home">
          <Link to="/" className="text-blue-500 hover:underline">
            Home
          </Link>
        </li>
        {paths.map((path, index) => {
          const isLast = index === paths.length - 1;
          // Create unique key using full path up to this point
          const fullPath = `/${paths.slice(0, index + 1).join("/")}`;

          return (
            <li key={fullPath} className="flex gap-2">
              <span>/</span>
              {isLast ? (
                <span className="font-medium">{path}</span>
              ) : (
                <Link to={fullPath} className="text-blue-500 hover:underline">
                  {path}
                </Link>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
