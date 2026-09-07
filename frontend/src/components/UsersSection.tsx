import { Fragment, useMemo, useState } from 'react';
import { Plus, KeyRound, UserMinus, UserPlus, Mail, X } from 'lucide-react';
import { useUsers } from '../hooks/useUsers';
import { useProperties } from '../hooks/useProperties';
import { useAuth } from '../hooks/useAuth';
import { usersApi } from '../services/api';
import type { ManagedUser } from '../types/api';
import { ASSIGNABLE_ROLES, formatRoleLabel } from '../utils/authorization';

type CreateForm = {
  email: string;
  fullName: string;
  role: string;
  propertyIds: string[];
};

const emptyForm: CreateForm = { email: '', fullName: '', role: 'Manager', propertyIds: [] };

export function UsersSection() {
  const { users, loading, error, reload } = useUsers();
  const { properties } = useProperties();
  const { user: currentUser } = useAuth();

  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState<CreateForm>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [tempPasswordModal, setTempPasswordModal] = useState<{ email: string; password: string } | null>(null);
  const [editingProperties, setEditingProperties] = useState<ManagedUser | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const propertyById = useMemo(() => {
    const map = new Map<string, string>();
    properties.forEach((p) => map.set(p.id, p.name));
    return map;
  }, [properties]);

  const handleCreate = async () => {
    setActionError(null);
    if (!form.email || !form.fullName) {
      setActionError('Email and name are required.');
      return;
    }
    setSaving(true);
    try {
      const res = await usersApi.create(form);
      if (res.data.success && res.data.data) {
        setTempPasswordModal({
          email: res.data.data.user.email,
          password: res.data.data.tempPassword,
        });
        setForm(emptyForm);
        setShowForm(false);
        await reload();
      } else {
        setActionError(res.data.error ?? 'Failed to create user');
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { error?: string } }; message?: string };
      setActionError(e.response?.data?.error ?? e.message ?? 'Failed to create user');
    } finally {
      setSaving(false);
    }
  };

  const handleResetPassword = async (u: ManagedUser) => {
    setActionError(null);
    if (!confirm(`Reset password for ${u.email}? They will need to set a new password on next login.`)) return;
    try {
      const res = await usersApi.resetPassword(u.id);
      if (res.data.success && res.data.data) {
        setTempPasswordModal({ email: u.email, password: res.data.data.tempPassword });
        await reload();
      } else {
        setActionError(res.data.error ?? 'Failed to reset password');
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { error?: string } }; message?: string };
      setActionError(e.response?.data?.error ?? e.message ?? 'Failed to reset password');
    }
  };

  const handleToggleActive = async (u: ManagedUser) => {
    setActionError(null);
    const next = !u.isActive;
    if (!next && !confirm(`Deactivate ${u.email}? They will not be able to log in.`)) return;
    try {
      const res = await usersApi.setActive(u.id, next);
      if (res.data.success) {
        await reload();
      } else {
        setActionError(res.data.error ?? 'Failed to update user');
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { error?: string } }; message?: string };
      setActionError(e.response?.data?.error ?? e.message ?? 'Failed to update user');
    }
  };

  const handleSaveProperties = async (
    u: ManagedUser,
    propertyIds: string[],
    summaryPropertyIds: string[],
    alertPropertyIds: string[],
  ) => {
    setActionError(null);
    try {
      const res = await usersApi.setProperties(u.id, propertyIds, summaryPropertyIds, alertPropertyIds);
      if (res.data.success) {
        setEditingProperties(null);
        await reload();
      } else {
        setActionError(res.data.error ?? 'Failed to update properties');
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { error?: string } }; message?: string };
      setActionError(e.response?.data?.error ?? e.message ?? 'Failed to update properties');
    }
  };

  return (
    <div className="card" style={{ marginBottom: 24 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <h2 style={{ fontSize: 16 }}>Users</h2>
        <button className="btn btn-primary" onClick={() => { setShowForm((s) => !s); setActionError(null); }}>
          <Plus size={16} /> Add User
        </button>
      </div>

      {actionError && (
        <div style={{ background: '#fef2f2', color: '#991b1b', padding: 10, borderRadius: 6, marginBottom: 12, fontSize: 13 }}>
          {actionError}
        </div>
      )}

      {showForm && (
        <div style={{
          background: 'var(--bg-primary)',
          border: '1px solid var(--border)',
          borderRadius: 'var(--radius)',
          padding: 20,
          marginBottom: 20,
        }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <div>
              <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>Email *</label>
              <input className="input" type="email" value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })} />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>Full Name *</label>
              <input className="input" value={form.fullName}
                onChange={(e) => setForm({ ...form, fullName: e.target.value })} />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>Role *</label>
              <select className="input" value={form.role}
                onChange={(e) => setForm({ ...form, role: e.target.value })}>
                {ASSIGNABLE_ROLES.map((r) => (
                  <option key={r} value={r}>{formatRoleLabel(r)}</option>
                ))}
              </select>
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>Properties</label>
              <PropertyMultiSelect
                allProperties={properties.map((p) => ({ id: p.id, name: p.name }))}
                selectedIds={form.propertyIds}
                onChange={(ids) => setForm({ ...form, propertyIds: ids })}
              />
            </div>
          </div>
          <div style={{ marginTop: 16, display: 'flex', gap: 8 }}>
            <button className="btn btn-primary" onClick={handleCreate} disabled={saving}>
              {saving ? 'Creating...' : 'Create User'}
            </button>
            <button className="btn btn-secondary" onClick={() => { setShowForm(false); setForm(emptyForm); }}>Cancel</button>
          </div>
        </div>
      )}

      {loading ? (
        <div className="loading">Loading users...</div>
      ) : error ? (
        <div className="empty-state"><p>{error}</p></div>
      ) : users.length === 0 ? (
        <div className="empty-state"><p>No users yet.</p></div>
      ) : (
        <div className="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Role</th>
                <th>Properties</th>
                <th>Summary email</th>
                <th>Status</th>
                <th style={{ textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => {
                const isSelf = currentUser?.id === u.id;
                const propNames = u.propertyIds
                  .map((id) => propertyById.get(id))
                  .filter(Boolean) as string[];
                return (
                  <tr key={u.id}>
                    <td style={{ fontWeight: 600 }}>
                      {u.fullName}{isSelf && <span style={{ color: 'var(--text-muted)', fontWeight: 400, fontSize: 12 }}> (you)</span>}
                    </td>
                    <td>{u.email}</td>
                    <td>{formatRoleLabel(u.role)}</td>
                    <td>
                      <button className="btn btn-secondary" style={{ fontSize: 12, padding: '4px 8px' }}
                        onClick={() => setEditingProperties(u)}>
                        {propNames.length === 0 ? 'None' : propNames.length === properties.length ? 'All' : `${propNames.length} selected`}
                      </button>
                    </td>
                    <td>
                      {u.summaryPropertyIds.length === 0 ? (
                        <span style={{ color: 'var(--text-muted)' }}>-</span>
                      ) : (
                        <span
                          title={u.summaryPropertyIds
                            .map((id) => propertyById.get(id))
                            .filter(Boolean)
                            .join(', ')}
                          style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 13 }}
                        >
                          <Mail size={14} />
                          {u.isActive
                            ? `${u.summaryPropertyIds.length}`
                            : <em style={{ color: 'var(--text-muted)' }}>paused</em>}
                        </span>
                      )}
                    </td>
                    <td>
                      <span className={`badge ${u.isActive ? 'badge-low' : 'badge-high'}`}>
                        {u.isActive ? 'Active' : 'Inactive'}
                      </span>
                      {u.mustChangePassword && (
                        <span className="badge badge-medium" style={{ marginLeft: 6 }}>Pending PW reset</span>
                      )}
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <button className="btn btn-secondary" style={{ marginRight: 6 }} title="Reset password"
                        onClick={() => handleResetPassword(u)}>
                        <KeyRound size={14} />
                      </button>
                      <button className="btn btn-secondary" title={u.isActive ? 'Deactivate' : 'Activate'}
                        disabled={isSelf}
                        onClick={() => handleToggleActive(u)}>
                        {u.isActive ? <UserMinus size={14} /> : <UserPlus size={14} />}
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {tempPasswordModal && (
        <TempPasswordModal
          email={tempPasswordModal.email}
          password={tempPasswordModal.password}
          onClose={() => setTempPasswordModal(null)}
        />
      )}

      {editingProperties && (
        <PropertyAssignmentModal
          user={editingProperties}
          allProperties={properties.map((p) => ({ id: p.id, name: p.name }))}
          onClose={() => setEditingProperties(null)}
          onSave={(ids, summaryIds, alertIds) =>
            handleSaveProperties(editingProperties, ids, summaryIds, alertIds)}
        />
      )}
    </div>
  );
}

function PropertyMultiSelect({
  allProperties,
  selectedIds,
  onChange,
}: {
  allProperties: { id: string; name: string }[];
  selectedIds: string[];
  onChange: (ids: string[]) => void;
}) {
  const toggle = (id: string) => {
    onChange(selectedIds.includes(id) ? selectedIds.filter((x) => x !== id) : [...selectedIds, id]);
  };
  if (allProperties.length === 0) {
    return <div style={{ fontSize: 13, color: 'var(--text-muted)' }}>No properties exist yet.</div>;
  }
  return (
    <div style={{ maxHeight: 120, overflowY: 'auto', border: '1px solid var(--border)', borderRadius: 6, padding: 8 }}>
      {allProperties.map((p) => (
        <label key={p.id} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, padding: '2px 0' }}>
          <input
            type="checkbox"
            checked={selectedIds.includes(p.id)}
            onChange={() => toggle(p.id)}
          />
          {p.name}
        </label>
      ))}
    </div>
  );
}

function PropertyAssignmentModal({
  user,
  allProperties,
  onClose,
  onSave,
}: {
  user: ManagedUser;
  allProperties: { id: string; name: string }[];
  onClose: () => void;
  onSave: (ids: string[], summaryIds: string[], alertIds: string[]) => Promise<void> | void;
}) {
  const [selected, setSelected] = useState<string[]>(user.propertyIds);
  const [summarySelected, setSummarySelected] = useState<string[]>(user.summaryPropertyIds);
  const [alertSelected, setAlertSelected] = useState<string[]>(user.alertPropertyIds);
  const [saving, setSaving] = useState(false);

  const toggleAccess = (id: string) => {
    if (selected.includes(id)) {
      // Removing access removes the summary with it: the server rejects a summary flag on
      // an unassigned property, and silently mailing reports about somewhere you cannot
      // open would be the wrong default anyway.
      setSelected(selected.filter((x) => x !== id));
      setSummarySelected(summarySelected.filter((x) => x !== id));
      setAlertSelected(alertSelected.filter((x) => x !== id));
    } else {
      setSelected([...selected, id]);
    }
  };

  const toggleSummary = (id: string) => {
    if (summarySelected.includes(id)) {
      setSummarySelected(summarySelected.filter((x) => x !== id));
    } else {
      // Ticking the summary implies access, so grant it rather than failing validation.
      setSummarySelected([...summarySelected, id]);
      if (!selected.includes(id)) setSelected([...selected, id]);
    }
  };

  const toggleAlerts = (id: string) => {
    if (alertSelected.includes(id)) {
      setAlertSelected(alertSelected.filter((x) => x !== id));
    } else {
      setAlertSelected([...alertSelected, id]);
      if (!selected.includes(id)) setSelected([...selected, id]);
    }
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await onSave(selected, summarySelected, alertSelected);
    } finally {
      setSaving(false);
    }
  };

  return (
    <ModalShell onClose={onClose} title={`Access for ${user.fullName}`}>
      {allProperties.length === 0 ? (
        <div style={{ fontSize: 13, color: 'var(--text-muted)' }}>No properties exist yet.</div>
      ) : (
        <>
          <div style={{
            display: 'grid',
            gridTemplateColumns: '1fr 62px 86px 86px',
            gap: '6px 8px',
            alignItems: 'center',
            fontSize: 13,
          }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>Property</div>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', textAlign: 'center' }}>Access</div>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', textAlign: 'center' }}>Summary email</div>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', textAlign: 'center' }}>Failure alerts</div>
            {allProperties.map((p) => (
              <Fragment key={p.id}>
                <div>{p.name}</div>
                <div style={{ textAlign: 'center' }}>
                  <input
                    type="checkbox"
                    aria-label={`Access to ${p.name}`}
                    checked={selected.includes(p.id)}
                    onChange={() => toggleAccess(p.id)}
                  />
                </div>
                <div style={{ textAlign: 'center' }}>
                  <input
                    type="checkbox"
                    aria-label={`Email ${user.fullName} the summary for ${p.name}`}
                    checked={summarySelected.includes(p.id)}
                    onChange={() => toggleSummary(p.id)}
                  />
                </div>
                <div style={{ textAlign: 'center' }}>
                  <input
                    type="checkbox"
                    aria-label={`Alert ${user.fullName} when pickup fails for ${p.name}`}
                    checked={alertSelected.includes(p.id)}
                    onChange={() => toggleAlerts(p.id)}
                  />
                </div>
              </Fragment>
            ))}
          </div>
          <p style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 12, lineHeight: 1.5 }}>
            <strong>Summary email</strong> is the nightly report. <strong>Failure alerts</strong>
            tell you when pickup breaks or a report never arrives &mdash; usually just you, not
            the board. Both are worked out when mail is sent, so deactivating this user or
            removing a property stops it straight away.
            {!user.isActive && (
              <><br /><strong>This user is inactive, so they receive nothing right now.</strong></>
            )}
          </p>
        </>
      )}
      <div style={{ marginTop: 16, display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
        <button className="btn btn-secondary" onClick={onClose}>Cancel</button>
        <button className="btn btn-primary" onClick={handleSave} disabled={saving}>
          {saving ? 'Saving...' : 'Save'}
        </button>
      </div>
    </ModalShell>
  );
}

function TempPasswordModal({ email, password, onClose }: { email: string; password: string; onClose: () => void }) {
  const [copied, setCopied] = useState(false);
  const [confirmed, setConfirmed] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(password);
      setCopied(true);
    } catch {
      // ignore
    }
  };

  return (
    <ModalShell onClose={confirmed ? onClose : () => {}} title="Temporary password">
      <p style={{ fontSize: 13, color: 'var(--text-muted)', marginBottom: 12 }}>
        Share this password with <strong>{email}</strong> through a secure channel. It will be shown only once. The user will be required to change it on first login.
      </p>
      <div style={{ display: 'flex', gap: 8 }}>
        <input className="input" readOnly value={password} style={{ fontFamily: 'Consolas, monospace' }} />
        <button className="btn btn-secondary" onClick={copy}>{copied ? 'Copied' : 'Copy'}</button>
      </div>
      <div style={{ marginTop: 16, display: 'flex', justifyContent: 'flex-end' }}>
        {!confirmed ? (
          <button
            className="btn btn-primary"
            disabled={!copied}
            onClick={() => setConfirmed(true)}
            title={copied ? '' : 'Copy the password first'}
          >
            I've saved this password
          </button>
        ) : (
          <button className="btn btn-primary" onClick={onClose}>Close</button>
        )}
      </div>
    </ModalShell>
  );
}

function ModalShell({ children, onClose, title }: { children: React.ReactNode; onClose: () => void; title: string }) {
  return (
    <div style={{
      position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)',
      display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000,
    }}
    onClick={onClose}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          background: 'var(--bg-secondary)', borderRadius: 8, padding: 24,
          width: 'min(480px, 90vw)', boxShadow: '0 10px 30px rgba(0,0,0,0.2)',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <h3 style={{ fontSize: 16 }}>{title}</h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer' }}>
            <X size={18} />
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}
