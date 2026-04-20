import { useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useApiKeys } from '../hooks/useApiKeys';
import { apiKeysApi } from '../services/api';
import type { ApiKey } from '../types/api';

export function ApiKeysSection() {
  const { keys, loading, error, reload } = useApiKeys();
  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState('');
  const [saving, setSaving] = useState(false);
  const [rawKeyModal, setRawKeyModal] = useState<{ key: ApiKey; rawKey: string } | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const handleCreate = async () => {
    setActionError(null);
    if (!name.trim()) {
      setActionError('Name is required.');
      return;
    }
    setSaving(true);
    try {
      const res = await apiKeysApi.create({ name: name.trim() });
      if (res.data.success && res.data.data) {
        setRawKeyModal({ key: res.data.data.apiKey, rawKey: res.data.data.rawKey });
        setName('');
        setShowForm(false);
        await reload();
      } else {
        setActionError(res.data.error ?? 'Failed to create key');
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { error?: string } }; message?: string };
      setActionError(e.response?.data?.error ?? e.message ?? 'Failed to create key');
    } finally {
      setSaving(false);
    }
  };

  const handleRevoke = async (k: ApiKey) => {
    setActionError(null);
    if (!confirm(`Revoke "${k.name}"? Any integration using it will immediately stop working.`)) return;
    try {
      const res = await apiKeysApi.revoke(k.id);
      if (res.data.success) await reload();
      else setActionError(res.data.error ?? 'Failed to revoke key');
    } catch (err: unknown) {
      const e = err as { response?: { data?: { error?: string } }; message?: string };
      setActionError(e.response?.data?.error ?? e.message ?? 'Failed to revoke key');
    }
  };

  return (
    <div className="card" style={{ marginBottom: 24 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <div>
          <h2 style={{ fontSize: 16 }}>API Keys</h2>
          <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 4 }}>
            Used by integrations (Power Automate, scripts) to post to <code>/api/v1/ingest/report</code>.
            Each key is tenant-scoped and behaves as an admin.
          </div>
        </div>
        <button className="btn btn-primary" onClick={() => { setShowForm((s) => !s); setActionError(null); }}>
          <Plus size={16} /> Generate Key
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
          <label style={{ display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>
            Name (for your reference)
          </label>
          <input className="input" placeholder="e.g. Palm Cove Flow" value={name}
            onChange={(e) => setName(e.target.value)} />
          <div style={{ marginTop: 16, display: 'flex', gap: 8 }}>
            <button className="btn btn-primary" onClick={handleCreate} disabled={saving}>
              {saving ? 'Generating...' : 'Generate'}
            </button>
            <button className="btn btn-secondary" onClick={() => { setShowForm(false); setName(''); }}>Cancel</button>
          </div>
        </div>
      )}

      {loading ? (
        <div className="loading">Loading keys...</div>
      ) : error ? (
        <div className="empty-state"><p>{error}</p></div>
      ) : keys.length === 0 ? (
        <div className="empty-state"><p>No API keys yet.</p></div>
      ) : (
        <div className="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Prefix</th>
                <th>Created</th>
                <th>Last Used</th>
                <th>Status</th>
                <th style={{ textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {keys.map((k) => (
                <tr key={k.id}>
                  <td style={{ fontWeight: 600 }}>{k.name}</td>
                  <td><code>{k.keyPrefix}…</code></td>
                  <td>{new Date(k.createdAt).toLocaleString()}</td>
                  <td>{k.lastUsedAt ? new Date(k.lastUsedAt).toLocaleString() : '—'}</td>
                  <td>
                    {k.revokedAt ? (
                      <span className="badge badge-high">Revoked</span>
                    ) : (
                      <span className="badge badge-low">Active</span>
                    )}
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    {!k.revokedAt && (
                      <button className="btn btn-secondary" onClick={() => handleRevoke(k)} title="Revoke">
                        <Trash2 size={14} />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {rawKeyModal && (
        <RawKeyModal
          name={rawKeyModal.key.name}
          rawKey={rawKeyModal.rawKey}
          onClose={() => setRawKeyModal(null)}
        />
      )}
    </div>
  );
}

function RawKeyModal({ name, rawKey, onClose }: { name: string; rawKey: string; onClose: () => void }) {
  const [copied, setCopied] = useState(false);
  const [confirmed, setConfirmed] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(rawKey);
      setCopied(true);
    } catch {
      // ignore
    }
  };

  return (
    <div
      style={{
        position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000,
      }}
      onClick={confirmed ? onClose : () => {}}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          background: 'var(--bg-secondary)', borderRadius: 8, padding: 24,
          width: 'min(560px, 92vw)', boxShadow: '0 10px 30px rgba(0,0,0,0.2)',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <h3 style={{ fontSize: 16 }}>API key: {name}</h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer' }}>
            <X size={18} />
          </button>
        </div>
        <p style={{ fontSize: 13, color: 'var(--text-muted)', marginBottom: 12 }}>
          Copy this key now. It cannot be retrieved after this dialog closes. If lost, revoke it and generate a new one.
        </p>
        <div style={{ display: 'flex', gap: 8 }}>
          <input className="input" readOnly value={rawKey}
            style={{ fontFamily: 'Consolas, monospace', fontSize: 12 }} />
          <button className="btn btn-secondary" onClick={copy}>{copied ? 'Copied' : 'Copy'}</button>
        </div>
        <details style={{ marginTop: 16, fontSize: 13 }}>
          <summary style={{ cursor: 'pointer' }}>How to use this key</summary>
          <div style={{ marginTop: 8, fontSize: 12, color: 'var(--text-muted)' }}>
            Send with the header <code>X-Api-Key: &lt;the key above&gt;</code> to any API endpoint. Example:
            <pre style={{ background: 'var(--bg-primary)', padding: 8, borderRadius: 4, marginTop: 8, overflowX: 'auto' }}>
{`POST /api/v1/ingest/report HTTP/1.1
Content-Type: application/json
X-Api-Key: ${rawKey}

{
  "propertyId": "<property-guid>",
  "fileName": "report.pdf",
  "pdfBase64": "<base64-encoded-pdf>",
  "externalId": "<optional-message-id-for-idempotency>"
}`}
            </pre>
          </div>
        </details>
        <div style={{ marginTop: 16, display: 'flex', justifyContent: 'flex-end' }}>
          {!confirmed ? (
            <button
              className="btn btn-primary"
              disabled={!copied}
              onClick={() => setConfirmed(true)}
              title={copied ? '' : 'Copy the key first'}
            >
              I've saved this key
            </button>
          ) : (
            <button className="btn btn-primary" onClick={onClose}>Close</button>
          )}
        </div>
      </div>
    </div>
  );
}
