import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Menu, X, Music2 } from "lucide-react";
import { Button } from "./ui/button";
import { useAuth } from "../auth/AuthContext";

export function Navbar() {
  const [isOpen, setIsOpen] = useState(false);
  const navigate = useNavigate();
  const { isAuthenticated, logout } = useAuth();

  return (
    <nav className="bg-primary">
      <div className="container mx-auto px-4">
        <div className="flex items-center justify-between h-16">
          {/* Logo */}
          <div className="flex items-center">
            <Music2 className="h-8 w-8 text-primary-foreground" />
            <span className="ml-2 text-xl font-bold text-primary-foreground">
              PlaylistPlayer
            </span>
          </div>

          {/* Desktop Menu */}
          <div className="hidden md:flex items-center space-x-4">
            <Button variant="ghost" onClick={() => navigate("/")}>
              Home
            </Button>
            <Button variant="ghost" onClick={() => navigate("/categories")}>
              Categories
            </Button>
            {isAuthenticated ? (
              <Button variant="outline" onClick={logout}>
                Logout
              </Button>
            ) : (
              <>
                <Button variant="outline" onClick={() => navigate("/login")}>
                  Login
                </Button>
                <Button variant="default" onClick={() => navigate("/register")}>
                  Register
                </Button>
              </>
            )}
          </div>

          {/* Mobile Menu Button */}
          <div className="md:hidden">
            <Button
              variant="ghost"
              size="icon"
              onClick={() => setIsOpen(!isOpen)}
              className="text-primary-foreground"
            >
              {isOpen ? <X /> : <Menu />}
            </Button>
          </div>
        </div>

        {/* Mobile Menu */}
        {isOpen && (
          <div className="md:hidden p-4">
            <div className="flex flex-col space-y-4">
              <Button variant="ghost" onClick={() => navigate("/")}>
                Home
              </Button>
              <Button variant="ghost" onClick={() => navigate("/categories")}>
                Categories
              </Button>
              {isAuthenticated ? (
                <Button variant="outline" onClick={logout}>
                  Logout
                </Button>
              ) : (
                <>
                  <Button variant="outline" onClick={() => navigate("/login")}>
                    Login
                  </Button>
                  <Button
                    variant="default"
                    onClick={() => navigate("/register")}
                  >
                    Register
                  </Button>
                </>
              )}
            </div>
          </div>
        )}
      </div>
    </nav>
  );
}
