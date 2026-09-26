import { NavLink } from "react-router-dom";
import { Gauge, PlusCircle, Settings, LogOut, LogIn } from "lucide-react";
import { useAuth } from "./AuthContext";

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-colors ${
    isActive ? "bg-ink text-paper" : "text-slate hover:bg-line/60"
  }`;

export function Navbar() {
  const { session, logout } = useAuth();

  return (
    <header className="border-b border-line bg-panel">
      <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
        <div className="flex items-center gap-2">
          <div className="flex h-8 w-8 items-center justify-center rounded-md bg-ink text-paper">
            <Gauge size={18} />
          </div>
          <div className="leading-tight">
            <p className="text-sm font-semibold tracking-tight">CreditWorks</p>
            <p className="text-xs text-slate">Vehicle Register</p>
          </div>
        </div>

        <nav className="flex items-center gap-1">
          <NavLink to="/" end className={linkClass}>
            <Gauge size={16} />
            Vehicles
          </NavLink>
          <NavLink to="/vehicles/new" className={linkClass}>
            <PlusCircle size={16} />
            Add vehicle
          </NavLink>
          <NavLink to="/admin/categories" className={linkClass}>
            <Settings size={16} />
            Categories
          </NavLink>

          {session ? (
            <button
              onClick={() => void logout()}
              className="ml-2 flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium text-slate hover:bg-line/60"
            >
              <LogOut size={16} />
              Log out
            </button>
          ) : (
            <NavLink to="/admin/login" className={linkClass}>
              <LogIn size={16} />
              Admin login
            </NavLink>
          )}
        </nav>
      </div>
    </header>
  );
}
