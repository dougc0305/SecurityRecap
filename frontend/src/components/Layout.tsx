import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import {
  LayoutDashboard, AlertTriangle, Car, FileText, MapPin,
  MessageSquare, Settings, LogOut, Shield, ClipboardList } from 'lucide-react';
import './Layout.css';
import { formatRoleLabel } from '../utils/authorization';

const navItems = [
  { to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/incidents', label: 'Incidents', icon: AlertTriangle },
  { to: '/vehicles', label: 'Vehicles', icon: Car },
  { to: '/reports', label: 'Reports', icon: FileText },
  { to: '/recap', label: 'Board Recap', icon: ClipboardList },
  { to: '/addresses', label: 'Addresses', icon: MapPin },
  { to: '/chat', label: 'AI Chat', icon: MessageSquare },
  { to: '/settings', label: 'Settings', icon: Settings },
];

export function Layout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="layout">
      <aside className="sidebar">
        <div className="sidebar-header">
          <Shield size={24} />
          <span className="sidebar-title">SecurityRecap</span>
        </div>
        <nav className="sidebar-nav">
          {navItems.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}
            >
              <Icon size={18} />
              <span>{label}</span>
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-footer">
          <div className="user-info">
            <span className="user-name">{user?.fullName}</span>
            <span className="user-role">{formatRoleLabel(user?.role)}</span>
          </div>
          <button className="logout-btn" onClick={handleLogout} title="Sign out">
            <LogOut size={18} />
          </button>
        </div>
      </aside>
      <main className="main-content">
        <Outlet />
      </main>
    </div>
  );
}
