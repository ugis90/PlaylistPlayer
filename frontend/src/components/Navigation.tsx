import React, { useState } from "react";
import { Link, useNavigate, useLocation } from "react-router-dom";
import {
  Menu,
  X,
  Home,
  Car,
  Map as MapIcon,
  LogOut,
  Shield,
  Users,
  UserPlus,
  LogIn,
} from "lucide-react";
import { useAuth } from "../auth/AuthContext";

const Navigation = () => {
  const [isOpen, setIsOpen] = useState(false);
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const { isAuthenticated, logout, userInfo, hasRole } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const toggleMenu = () => setIsOpen(!isOpen);
  const closeMenu = () => setIsOpen(false);
  const toggleUserMenu = () => setIsUserMenuOpen(!isUserMenuOpen);
  const closeUserMenu = () => setIsUserMenuOpen(false);

  const handleLogout = async () => {
    closeUserMenu();
    closeMenu();
    await logout();
    navigate("/login");
  };

  const NavLink = ({
    to,
    icon: Icon,
    children,
    end = false,
  }: {
    to: string;
    icon: React.ElementType;
    children: React.ReactNode;
    end?: boolean;
  }) => {
    const isActive = end
      ? location.pathname === to
      : location.pathname.startsWith(to);
    return (
      <Link
        to={to}
        className={`px-3 py-2 rounded-md text-sm font-medium flex items-center ${isActive ? "bg-blue-50 text-blue-700" : "text-gray-700 hover:text-gray-900 hover:bg-gray-50"}`}
        onClick={closeMenu}
      >
        {" "}
        <Icon className="h-5 w-5 mr-2 flex-shrink-0" /> {children}{" "}
      </Link>
    );
  };
  return (
    <nav className="bg-white shadow-md">
      <div className="max-w-7xl mx-auto px-4 md:px-6 lg:px-8">
        <div className="flex justify-between h-16">
          {/* Logo and brand */}
          <div className="flex">
            <Link
              to="/"
              className="flex-shrink-0 flex items-center"
              onClick={closeMenu}
            >
              <Car className="h-8 w-8 text-blue-600" />
              <span className="ml-2 text-xl font-bold text-gray-900">
                Family Fleet
              </span>
            </Link>
          </div>

          {/* Desktop Nav Links */}
          <div className="hidden md:flex md:items-center md:space-x-1 lg:space-x-2">
            {isAuthenticated && (
              <>
                <NavLink to="/" icon={Home} end={true}>
                  Dashboard
                </NavLink>
                <NavLink to="/vehicles" icon={Car}>
                  Vehicles
                </NavLink>
                <NavLink to="/tracking" icon={MapIcon}>
                  GPS Tracking
                </NavLink>
                {hasRole(["ADMIN", "PARENT"]) && (
                  <NavLink to="/family-tracking" icon={Users}>
                    Family Tracking
                  </NavLink>
                )}
                {hasRole(["ADMIN", "PARENT"]) && (
                  <NavLink to="/family-management" icon={Users}>
                    Family Members
                  </NavLink>
                )}
                {hasRole("ADMIN") && (
                  <NavLink to="/admin" icon={Shield}>
                    Admin
                  </NavLink>
                )}
              </>
            )}
          </div>

          {/* User menu dropdown / Login/Register */}
          <div className="hidden md:ml-4 md:flex md:items-center">
            {isAuthenticated ? (
              <div className="ml-3 relative">
                <div>
                  <button
                    onClick={toggleUserMenu}
                    className="flex text-sm rounded-full focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
                    aria-expanded={isUserMenuOpen}
                    aria-haspopup="true"
                  >
                    <span className="sr-only">Open user menu</span>
                    <div className="h-8 w-8 rounded-full bg-blue-200 flex items-center justify-center ring-1 ring-blue-300">
                      <span className="text-blue-700 font-medium">
                        {userInfo?.username?.charAt(0).toUpperCase() ?? "U"}
                      </span>
                    </div>
                  </button>
                </div>
                {isUserMenuOpen && (
                  <div
                    className="origin-top-right absolute right-0 mt-2 w-56 rounded-md shadow-lg bg-white ring-1 ring-black ring-opacity-5 z-50 focus:outline-none"
                    role="menu"
                    aria-orientation="vertical"
                    tabIndex={-1}
                  >
                    <div className="py-1 border-b border-gray-100" role="none">
                      <div className="block px-4 py-2 text-sm text-gray-700">
                        <p className="font-medium truncate">
                          {userInfo?.username ?? "User"}
                        </p>
                        <p className="text-gray-500 truncate">
                          {userInfo?.email ?? ""}
                        </p>
                        <p className="text-xs mt-1 text-blue-600 font-semibold">
                          Role: {userInfo?.role ?? "Unknown"}
                        </p>
                      </div>
                    </div>
                    <div className="py-1" role="none">
                      <button
                        onClick={handleLogout}
                        className="w-full text-left block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 flex items-center"
                        role="menuitem"
                        tabIndex={-1}
                      >
                        <LogOut className="h-4 w-4 mr-2" /> Sign out
                      </button>
                    </div>
                  </div>
                )}
              </div>
            ) : (
              <div className="flex items-center space-x-2">
                <Link
                  to="/login"
                  className="px-3 py-2 rounded-md text-sm font-medium bg-white text-blue-600 border border-blue-600 hover:bg-blue-50 flex items-center"
                >
                  <LogIn className="h-4 w-4 mr-1" /> Log in
                </Link>
                <Link
                  to="/register"
                  className="px-3 py-2 rounded-md text-sm font-medium bg-blue-600 text-white hover:bg-blue-700 flex items-center"
                >
                  <UserPlus className="h-4 w-4 mr-1" /> Register
                </Link>
              </div>
            )}
          </div>

          {/* Mobile menu button */}
          <div className="flex items-center md:hidden">
            <button
              onClick={toggleMenu}
              className="inline-flex items-center justify-center p-2 rounded-md text-gray-400 hover:text-gray-500 hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-blue-500"
              aria-controls="mobile-menu"
              aria-expanded={isOpen}
            >
              <span className="sr-only">Open main menu</span>
              {isOpen ? (
                <X className="block h-6 w-6" />
              ) : (
                <Menu className="block h-6 w-6" />
              )}
            </button>
          </div>
        </div>
      </div>

      {/* Mobile menu */}
      {isOpen && (
        <div
          className="md:hidden bg-white border-t border-gray-200"
          id="mobile-menu"
        >
          <div className="pt-2 pb-3 space-y-1 px-2">
            {isAuthenticated ? (
              <>
                <NavLink to="/" icon={Home} end={true}>
                  Dashboard
                </NavLink>
                <NavLink to="/vehicles" icon={Car}>
                  Vehicles
                </NavLink>
                <NavLink to="/tracking" icon={MapIcon}>
                  GPS Tracking
                </NavLink>
                {hasRole(["ADMIN", "PARENT"]) && (
                  <NavLink to="/family-tracking" icon={Users}>
                    Family Tracking
                  </NavLink>
                )}
                {hasRole(["ADMIN", "PARENT"]) && (
                  <NavLink to="/family-management" icon={Users}>
                    Family Members
                  </NavLink>
                )}
                {hasRole("ADMIN") && (
                  <NavLink to="/admin" icon={Shield}>
                    Admin
                  </NavLink>
                )}
              </>
            ) : (
              <div className="px-1 py-2 space-y-2">
                <Link
                  to="/login"
                  className="block w-full px-3 py-2 rounded-md text-base font-medium text-center bg-white text-blue-600 border border-blue-600 hover:bg-blue-50"
                  onClick={closeMenu}
                >
                  Log in
                </Link>
                <Link
                  to="/register"
                  className="block w-full px-3 py-2 rounded-md text-base font-medium text-center bg-blue-600 text-white hover:bg-blue-700"
                  onClick={closeMenu}
                >
                  Register
                </Link>
              </div>
            )}
          </div>
          {/* Mobile User Menu */}
          {isAuthenticated && (
            <div className="pt-4 pb-3 border-t border-gray-200">
              <div className="flex items-center px-5">
                <div className="flex-shrink-0">
                  <div className="h-10 w-10 rounded-full bg-blue-200 flex items-center justify-center ring-1 ring-blue-300">
                    <span className="text-blue-700 font-medium">
                      {userInfo?.username?.charAt(0).toUpperCase() ?? "U"}
                    </span>
                  </div>
                </div>
                <div className="ml-3">
                  <div className="text-base font-medium text-gray-800 truncate">
                    {userInfo?.username ?? "User"}
                  </div>
                  <div className="text-sm font-medium text-gray-500 truncate">
                    {userInfo?.email ?? ""}
                  </div>
                  <div className="text-xs text-blue-600 font-semibold">
                    Role: {userInfo?.role ?? "Unknown"}
                  </div>
                </div>
              </div>
              <div className="mt-3 space-y-1 px-2">
                <button
                  onClick={handleLogout}
                  className="w-full text-left block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:text-gray-900 hover:bg-gray-50 flex items-center"
                >
                  <LogOut className="h-5 w-5 mr-2" /> Sign out
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </nav>
  );
};

export default Navigation;
