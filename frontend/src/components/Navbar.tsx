import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Menu, X, Music2, LogIn, UserPlus, Home } from "lucide-react";
import { Button } from "./ui/button";
import { useAuth } from "../auth/AuthContext";

export function Navbar() {
  const [isOpen, setIsOpen] = useState(false);
  const navigate = useNavigate();
  const { isAuthenticated, logout } = useAuth();

  return (
    <nav className="px-4 py-3 flex items-center justify-between">
      {/* Logo and title */}
      <div
        className="flex items-center space-x-2 cursor-pointer"
        onClick={() => navigate("/")}
      >
        <Music2 className="h-6 w-6 text-primary-foreground" />
        <span className="text-xl font-bold text-primary-foreground">
          PlaylistPlayer
        </span>
      </div>

      {/* Desktop Menu */}
      <div className="hidden md:flex items-center space-x-4">
        <Button variant="ghost" onClick={() => navigate("/")}>
          <Home className="h-4 w-4 mr-1" /> Home
        </Button>
        {isAuthenticated ? (
          <Button variant="outline" onClick={logout}>
            Logout
          </Button>
        ) : (
          <>
            <Button variant="outline" onClick={() => navigate("/login")}>
              <LogIn className="h-4 w-4 mr-1" />
              Login
            </Button>
            <Button variant="default" onClick={() => navigate("/register")}>
              <UserPlus className="h-4 w-4 mr-1" />
              Register
            </Button>
          </>
        )}
      </div>

      {/* Mobile Hamburger */}
      <div className="md:hidden">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => setIsOpen(!isOpen)}
          className="text-primary-foreground"
          aria-label="Toggle Menu"
        >
          {isOpen ? <X /> : <Menu />}
        </Button>
      </div>

      {/* Mobile Menu */}
      {isOpen && (
        <div className="md:hidden absolute top-16 left-0 w-full bg-gray-700 p-4 space-y-4">
          <Button variant="ghost" onClick={() => navigate("/")}>
            <Home className="h-4 w-4 mr-1" /> Home
          </Button>
          {isAuthenticated ? (
            <Button variant="outline" onClick={logout}>
              Logout
            </Button>
          ) : (
            <>
              <Button variant="outline" onClick={() => navigate("/login")}>
                <LogIn className="h-4 w-4 mr-1" /> Login
              </Button>
              <Button variant="default" onClick={() => navigate("/register")}>
                <UserPlus className="h-4 w-4 mr-1" /> Register
              </Button>
            </>
          )}
        </div>
      )}
    </nav>
  );
}
