import { useState } from 'react';
import { useProperties } from '../hooks/useProperties';
import { useAuth } from '../hooks/useAuth';
import { propertiesApi } from '../services/api';
import { Plus } from 'lucide-react';
import { formatRoleLabel, isAdmin } from '../utils/authorization';
import { UsersSection } from '../components/UsersSection';
import { ApiKeysSection } from '../components/ApiKeysSection';
import { Link } from 'react-router-dom';

export function SettingsPage() {
  const { user } = useAuth();
  const { properties, loading } = useProperties();
  const [showForm, setShowForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({
    name: '',
    address: '',
    city: '',
    state: '',
    zip: '',
    securityCompany: '',
    reportEmail: '',
    timezone: 'America/New_York',
  });
  const canManageProperties = isAdmin(user);

  const handleCreate = async () => {
    if (!canManageProperties) {
      return;
    }

    setSaving(true);
    try {
      await propertiesApi.create(form);
      setShowForm(false);
      setForm({ name: '', address: '', city: '', state: '', zip: '', securityCompany: '', reportEmail: '', timezone: 'America/New_York' });
      window.location.reload();
    } catch (err) {
      alert('Failed to create property');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Settings</h1>
      </div>

      <div className="card" style={{ marginBottom: 24 }}>
        <h2 style={{ fontSize: 16, marginBottom: 16 }}>Account</h2>
        <div style={{ display: 'grid', gridTemplateColumns: '120px 1fr', gap: '8px 16px', fontSize: 14 }}>
          <span style={{ color: 'var(--text-muted)' }}>Name</span>
          <span>{user?.fullName}</span>
          <span style={{ color: 'var(--text-muted)' }}>Email</span>
          <span>{user?.email}</span>
          <span style={{ color: 'var(--text-muted)' }}>Role</span>
          <span>{formatRoleLabel(user?.role)}</span>
        </div>
        <div style={{ marginTop: 16 }}>
          <Link to="/change-password" className="btn btn-secondary" style={{ fontSize: 13 }}>Change password</Link>
        </div>
      </div>

      {canManageProperties && <UsersSection />}
      {canManageProperties && <ApiKeysSection />}

      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <h2 style={{ fontSize: 16 }}>Properties</h2>
          {canManageProperties && (
            <button className="btn btn-primary" onClick={() => setShowForm(!showForm)}>
              <Plus size={16} /> Add Property
            </button>
          )}
        </div>

        {!canManageProperties && (
          <p style={{ color: 'var(--text-muted)', marginBottom: 16 }}>
            Property creation and updates are limited to system administrators.
          </p>
        )}

        {canManageProperties && showForm && (
          <div style={{
            background: 'var(--bg-primary)',
            border: '1px solid var(--border)',
            borderRadius: 'var(--radius)',
            padding: 20,
            marginBottom: 20,
          }}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
              <div>
                <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>Name *</label>
                <input className="input" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>Address *</label>
                <input className="input" value={form.address} onChange={(e) => setForm({ ...form, address: e.target.value })} />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>City *</label>
                <input className="input" value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>State *</label>
                <input className="input" maxLength={2} value={form.state} onChange={(e) => setForm({ ...form, state: e.target.value })} />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>ZIP *</label>
                <input className="input" value={form.zip} onChange={(e) => setForm({ ...form, zip: e.target.value })} />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>Security Company</label>
                <input className="input" value={form.securityCompany} onChange={(e) => setForm({ ...form, securityCompany: e.target.value })} />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>Report Email</label>
                <input className="input" type="email" value={form.reportEmail} onChange={(e) => setForm({ ...form, reportEmail: e.target.value })} />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>Timezone</label>
                <input className="input" value={form.timezone} onChange={(e) => setForm({ ...form, timezone: e.target.value })} />
              </div>
            </div>
            <div style={{ marginTop: 16, display: 'flex', gap: 8 }}>
              <button className="btn btn-primary" onClick={handleCreate} disabled={saving}>
                {saving ? 'Creating...' : 'Create Property'}
              </button>
              <button className="btn btn-secondary" onClick={() => setShowForm(false)}>Cancel</button>
            </div>
          </div>
        )}

        {loading ? (
          <div className="loading">Loading...</div>
        ) : properties.length === 0 ? (
          <div className="empty-state">
            <p>No properties yet. Add one to get started.</p>
          </div>
        ) : (
          <div className="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Address</th>
                  <th>City</th>
                  <th>State</th>
                  <th>Security Co.</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {properties.map((p) => (
                  <tr key={p.id}>
                    <td style={{ fontWeight: 600 }}>{p.name}</td>
                    <td>{p.address}</td>
                    <td>{p.city}</td>
                    <td>{p.state}</td>
                    <td>{p.securityCompany || '-'}</td>
                    <td>
                      <span className={`badge ${p.isActive ? 'badge-low' : 'badge-high'}`}>
                        {p.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
