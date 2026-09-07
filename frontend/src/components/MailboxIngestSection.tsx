import { useCallback, useEffect, useState } from 'react';
import { Mail, RefreshCw, PlugZap, Trash2 } from 'lucide-react';
import { mailboxIngestApi } from '../services/api';
import { useProperties } from '../hooks/useProperties';
import type {
  MailboxIngestConfig,
  MailboxPollResult,
  SaveMailboxIngestConfigRequest,
} from '../types/api';

type FormState = Omit<SaveMailboxIngestConfigRequest, 'propertyId'> & {
  graphClientSecret: string;
};

const EMPTY_FORM: FormState = {
  graphTenantId: '',
  graphClientId: '',
  graphClientSecret: '',
  mailboxAddress: '',
  folderName: 'inbox',
  fromAddress: '',
  subjectContains: '',
  attachmentNameContains: '',
  lookbackDays: 3,
  pollIntervalMinutes: 60,
  activeWindowStart: '08:30',
  activeWindowEnd: '10:00',
  activeWindowPollMinutes: 5,
  scheduleTimeZone: 'America/New_York',
  staleAfterHours: 26,
  markAsRead: true,
  moveToFolder: '',
  sendSummaryEmail: false,
  summaryRecipients: [],
  summarySubjectPrefix: '',
  isEnabled: true,
};

const labelStyle: React.CSSProperties = {
  display: 'block',
  fontSize: 12,
  color: 'var(--text-muted)',
  marginBottom: 4,
};

const hintStyle: React.CSSProperties = {
  fontSize: 11,
  color: 'var(--text-muted)',
  marginTop: 4,
};

function errorFrom(err: unknown, fallback: string): string {
  const e = err as {
    response?: { data?: { error?: string; title?: string; errors?: Record<string, string[]> } };
    message?: string;
  };
  const data = e.response?.data;
  const validation = data?.errors ? Object.values(data.errors).flat().join(' ') : undefined;
  return data?.error ?? validation ?? data?.title ?? e.message ?? fallback;
}

function formatTimestamp(value: string | null): string {
  if (!value) return 'Never';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? 'Never' : parsed.toLocaleString();
}

export function MailboxIngestSection() {
  const { properties, selectedPropertyId, setSelectedPropertyId } = useProperties();
  const [config, setConfig] = useState<MailboxIngestConfig | null>(null);
  const [form, setForm] = useState<FormState>(EMPTY_FORM);
  const [recipientsText, setRecipientsText] = useState('');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [busyAction, setBusyAction] = useState<'verify' | 'poll' | null>(null);
  const [message, setMessage] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null);
  const [pollResult, setPollResult] = useState<MailboxPollResult | null>(null);

  const applyConfig = useCallback((loaded: MailboxIngestConfig | null) => {
    setConfig(loaded);
    if (!loaded) {
      setForm(EMPTY_FORM);
      setRecipientsText('');
      return;
    }
    setForm({
      graphTenantId: loaded.graphTenantId,
      graphClientId: loaded.graphClientId,
      // Never prefilled: the API does not return the stored secret. Leaving it blank
      // on save keeps whatever is already stored.
      graphClientSecret: '',
      mailboxAddress: loaded.mailboxAddress,
      folderName: loaded.folderName,
      fromAddress: loaded.fromAddress ?? '',
      subjectContains: loaded.subjectContains ?? '',
      attachmentNameContains: loaded.attachmentNameContains ?? '',
      lookbackDays: loaded.lookbackDays,
      pollIntervalMinutes: loaded.pollIntervalMinutes,
      activeWindowStart: loaded.activeWindowStart?.slice(0, 5) ?? '',
      activeWindowEnd: loaded.activeWindowEnd?.slice(0, 5) ?? '',
      activeWindowPollMinutes: loaded.activeWindowPollMinutes,
      scheduleTimeZone: loaded.scheduleTimeZone ?? '',
      staleAfterHours: loaded.staleAfterHours,
      markAsRead: loaded.markAsRead,
      moveToFolder: loaded.moveToFolder ?? '',
      sendSummaryEmail: loaded.sendSummaryEmail,
      summaryRecipients: loaded.summaryRecipients,
      summarySubjectPrefix: loaded.summarySubjectPrefix ?? '',
      isEnabled: loaded.isEnabled,
    });
    setRecipientsText(loaded.summaryRecipients.join(', '));
  }, []);

  useEffect(() => {
    if (!selectedPropertyId) return;
    setLoading(true);
    setMessage(null);
    setPollResult(null);
    mailboxIngestApi
      .get(selectedPropertyId)
      .then((res) => applyConfig(res.data.success ? res.data.data : null))
      .catch((err) => setMessage({ kind: 'error', text: errorFrom(err, 'Failed to load configuration') }))
      .finally(() => setLoading(false));
  }, [selectedPropertyId, applyConfig]);

  const parseRecipients = (text: string) =>
    text
      .split(/[,;\n]/)
      .map((r) => r.trim())
      .filter(Boolean);

  const handleSave = async () => {
    if (!selectedPropertyId) return;
    setSaving(true);
    setMessage(null);
    try {
      const payload: SaveMailboxIngestConfigRequest = {
        propertyId: selectedPropertyId,
        graphTenantId: form.graphTenantId.trim(),
        graphClientId: form.graphClientId.trim(),
        graphClientSecret: form.graphClientSecret.trim() || null,
        mailboxAddress: form.mailboxAddress.trim(),
        folderName: form.folderName.trim() || 'inbox',
        fromAddress: form.fromAddress?.trim() || null,
        subjectContains: form.subjectContains?.trim() || null,
        attachmentNameContains: form.attachmentNameContains?.trim() || null,
        lookbackDays: form.lookbackDays,
        pollIntervalMinutes: form.pollIntervalMinutes,
        activeWindowStart: form.activeWindowStart || null,
        activeWindowEnd: form.activeWindowEnd || null,
        activeWindowPollMinutes: form.activeWindowPollMinutes,
        scheduleTimeZone: form.scheduleTimeZone?.trim() || null,
        staleAfterHours: form.staleAfterHours,
        markAsRead: form.markAsRead,
        moveToFolder: form.moveToFolder?.trim() || null,
        sendSummaryEmail: form.sendSummaryEmail,
        summaryRecipients: parseRecipients(recipientsText),
        summarySubjectPrefix: form.summarySubjectPrefix?.trim() || null,
        isEnabled: form.isEnabled,
      };

      const res = await mailboxIngestApi.save(payload);
      if (res.data.success && res.data.data) {
        applyConfig(res.data.data);
        setMessage({ kind: 'ok', text: 'Configuration saved.' });
      } else {
        setMessage({ kind: 'error', text: res.data.error ?? 'Failed to save configuration' });
      }
    } catch (err) {
      setMessage({ kind: 'error', text: errorFrom(err, 'Failed to save configuration') });
    } finally {
      setSaving(false);
    }
  };

  const handleVerify = async () => {
    if (!selectedPropertyId) return;
    setBusyAction('verify');
    setMessage(null);
    try {
      const res = await mailboxIngestApi.verify(selectedPropertyId);
      if (res.data.success && res.data.data) {
        setMessage({ kind: 'ok', text: `Connected to ${res.data.data.mailbox}` });
      } else {
        setMessage({ kind: 'error', text: res.data.error ?? 'Connection test failed' });
      }
    } catch (err) {
      setMessage({ kind: 'error', text: errorFrom(err, 'Connection test failed') });
    } finally {
      setBusyAction(null);
    }
  };

  const handlePoll = async () => {
    if (!selectedPropertyId) return;
    setBusyAction('poll');
    setMessage(null);
    setPollResult(null);
    try {
      const res = await mailboxIngestApi.poll(selectedPropertyId);
      if (res.data.success && res.data.data) {
        const result = res.data.data;
        setPollResult(result);
        setMessage(
          result.succeeded
            ? {
                kind: 'ok',
                text: `Examined ${result.messagesExamined} message(s); ingested ${result.reportsIngested} report(s).`,
              }
            : { kind: 'error', text: result.error ?? 'Poll failed' }
        );
        const refreshed = await mailboxIngestApi.get(selectedPropertyId);
        if (refreshed.data.success) applyConfig(refreshed.data.data);
      } else {
        setMessage({ kind: 'error', text: res.data.error ?? 'Poll failed' });
      }
    } catch (err) {
      setMessage({ kind: 'error', text: errorFrom(err, 'Poll failed') });
    } finally {
      setBusyAction(null);
    }
  };

  const handleDelete = async () => {
    if (!selectedPropertyId || !config) return;
    if (!confirm('Remove this mailbox configuration? Reports will have to be uploaded manually again.')) return;
    try {
      await mailboxIngestApi.remove(selectedPropertyId);
      applyConfig(null);
      setPollResult(null);
      setMessage({ kind: 'ok', text: 'Configuration removed.' });
    } catch (err) {
      setMessage({ kind: 'error', text: errorFrom(err, 'Failed to remove configuration') });
    }
  };

  const secretPlaceholder = config?.hasClientSecret
    ? 'Stored — leave blank to keep it'
    : 'Client secret value from the app registration';

  return (
    <div className="card" style={{ marginBottom: 24 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 16, gap: 16 }}>
        <div>
          <h2 style={{ fontSize: 16, display: 'flex', alignItems: 'center', gap: 8 }}>
            <Mail size={16} /> Automated Report Pickup
          </h2>
          <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 4, maxWidth: 640 }}>
            Watches a mailbox for the nightly patrol report, ingests the PDF attachment, compares it
            against this property&apos;s history, and optionally emails the summary to the board.
            Requires an Entra app registration with the <code>Mail.Read</code> application permission
            (plus <code>Mail.Send</code> if the summary email is enabled).
          </div>
        </div>
        <select
          className="select"
          value={selectedPropertyId}
          onChange={(e) => setSelectedPropertyId(e.target.value)}
        >
          {properties.map((p) => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>
      </div>

      {message && (
        <div
          style={{
            padding: '10px 14px',
            borderRadius: 'var(--radius)',
            marginBottom: 16,
            fontSize: 13,
            border: '1px solid',
            borderColor: message.kind === 'ok' ? 'var(--success, #16a34a)' : 'var(--danger, #dc2626)',
            color: message.kind === 'ok' ? 'var(--success, #16a34a)' : 'var(--danger, #dc2626)',
          }}
        >
          {message.text}
        </div>
      )}

      {loading ? (
        <div className="loading">Loading configuration...</div>
      ) : (
        <>
          {config && (
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
                gap: 12,
                padding: 14,
                marginBottom: 20,
                background: 'var(--bg-primary)',
                border: '1px solid var(--border)',
                borderRadius: 'var(--radius)',
                fontSize: 13,
              }}
            >
              <div>
                <div style={labelStyle}>Status</div>
                <span className={`badge ${config.isEnabled ? 'badge-low' : 'badge-high'}`}>
                  {config.isEnabled ? 'Enabled' : 'Disabled'}
                </span>
              </div>
              <div>
                <div style={labelStyle}>Last checked</div>
                {formatTimestamp(config.lastPolledAt)}
              </div>
              <div>
                <div style={labelStyle}>Last successful check</div>
                {formatTimestamp(config.lastSuccessAt)}
              </div>
              <div>
                <div style={labelStyle}>Newest report seen</div>
                {formatTimestamp(config.lastMessageReceivedAt)}
              </div>
              <div>
                <div style={labelStyle}>Last report ingested</div>
                {formatTimestamp(config.lastReportIngestedAt)}
              </div>
              {config.lastError && (
                <div style={{ gridColumn: '1 / -1' }}>
                  <div style={labelStyle}>
                    Last error{config.consecutiveFailures > 1 ? ` (${config.consecutiveFailures} in a row)` : ''}
                  </div>
                  <div style={{ color: 'var(--danger, #dc2626)' }}>{config.lastError}</div>
                </div>
              )}
            </div>
          )}

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <div>
              <label style={labelStyle}>Directory (tenant) ID *</label>
              <input
                className="input"
                value={form.graphTenantId}
                onChange={(e) => setForm({ ...form, graphTenantId: e.target.value })}
              />
            </div>
            <div>
              <label style={labelStyle}>Application (client) ID *</label>
              <input
                className="input"
                value={form.graphClientId}
                onChange={(e) => setForm({ ...form, graphClientId: e.target.value })}
              />
            </div>
            <div style={{ gridColumn: '1 / -1' }}>
              <label style={labelStyle}>
                Client secret {config?.hasClientSecret ? '(stored)' : '*'}
              </label>
              <input
                className="input"
                type="password"
                autoComplete="new-password"
                placeholder={secretPlaceholder}
                value={form.graphClientSecret}
                onChange={(e) => setForm({ ...form, graphClientSecret: e.target.value })}
              />
              <div style={hintStyle}>
                Encrypted before storage and never sent back to this page.
              </div>
            </div>
            <div>
              <label style={labelStyle}>Mailbox address *</label>
              <input
                className="input"
                type="email"
                placeholder="reports@example.com"
                value={form.mailboxAddress}
                onChange={(e) => setForm({ ...form, mailboxAddress: e.target.value })}
              />
            </div>
            <div>
              <label style={labelStyle}>Folder</label>
              <input
                className="input"
                placeholder="inbox"
                value={form.folderName}
                onChange={(e) => setForm({ ...form, folderName: e.target.value })}
              />
            </div>
          </div>

          <h3 style={{ fontSize: 13, margin: '24px 0 12px', color: 'var(--text-muted)' }}>
            Which emails to pick up
          </h3>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
            <div>
              <label style={labelStyle}>From address</label>
              <input
                className="input"
                type="email"
                placeholder="Any sender"
                value={form.fromAddress ?? ''}
                onChange={(e) => setForm({ ...form, fromAddress: e.target.value })}
              />
            </div>
            <div>
              <label style={labelStyle}>Subject contains</label>
              <input
                className="input"
                placeholder="Any subject"
                value={form.subjectContains ?? ''}
                onChange={(e) => setForm({ ...form, subjectContains: e.target.value })}
              />
            </div>
            <div>
              <label style={labelStyle}>Attachment name contains</label>
              <input
                className="input"
                placeholder="Any PDF"
                value={form.attachmentNameContains ?? ''}
                onChange={(e) => setForm({ ...form, attachmentNameContains: e.target.value })}
              />
            </div>
            <div>
              <label style={labelStyle}>Check every (minutes, outside the window)</label>
              <input
                className="input"
                type="number"
                min={1}
                max={1440}
                value={form.pollIntervalMinutes}
                onChange={(e) => setForm({ ...form, pollIntervalMinutes: Number(e.target.value) })}
              />
            </div>
            <div>
              <label style={labelStyle}>First-run lookback (days)</label>
              <input
                className="input"
                type="number"
                min={1}
                max={90}
                value={form.lookbackDays}
                onChange={(e) => setForm({ ...form, lookbackDays: Number(e.target.value) })}
              />
              <div style={hintStyle}>How far back to reach on the very first check.</div>
            </div>
            <div>
              <label style={labelStyle}>Alert if no report for (hours)</label>
              <input
                className="input"
                type="number"
                min={0}
                max={720}
                value={form.staleAfterHours}
                onChange={(e) => setForm({ ...form, staleAfterHours: Number(e.target.value) })}
              />
              <div style={hintStyle}>
                Emails anyone flagged for failure alerts if a report goes missing. 0 turns it off.
              </div>
            </div>
            <div>
              <label style={labelStyle}>Move processed mail to</label>
              <input
                className="input"
                placeholder="Leave in place"
                value={form.moveToFolder ?? ''}
                onChange={(e) => setForm({ ...form, moveToFolder: e.target.value })}
              />
              <div style={hintStyle}>e.g. <code>archive</code></div>
            </div>
          </div>

          <div style={{ display: 'flex', gap: 20, marginTop: 16, flexWrap: 'wrap' }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13 }}>
              <input
                type="checkbox"
                checked={form.markAsRead}
                onChange={(e) => setForm({ ...form, markAsRead: e.target.checked })}
              />
              Mark processed mail as read
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13 }}>
              <input
                type="checkbox"
                checked={form.isEnabled}
                onChange={(e) => setForm({ ...form, isEnabled: e.target.checked })}
              />
              Check automatically on a schedule
            </label>
          </div>

          <h3 style={{ fontSize: 13, margin: '24px 0 12px', color: 'var(--text-muted)' }}>
            When the report is expected
          </h3>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1.4fr', gap: 12 }}>
            <div>
              <label style={labelStyle}>Window starts</label>
              <input
                className="input"
                type="time"
                value={form.activeWindowStart ?? ''}
                onChange={(e) => setForm({ ...form, activeWindowStart: e.target.value })}
              />
            </div>
            <div>
              <label style={labelStyle}>Window ends</label>
              <input
                className="input"
                type="time"
                value={form.activeWindowEnd ?? ''}
                onChange={(e) => setForm({ ...form, activeWindowEnd: e.target.value })}
              />
            </div>
            <div>
              <label style={labelStyle}>Check every (minutes, in window)</label>
              <input
                className="input"
                type="number"
                min={1}
                max={1440}
                value={form.activeWindowPollMinutes}
                onChange={(e) => setForm({ ...form, activeWindowPollMinutes: Number(e.target.value) })}
              />
            </div>
            <div>
              <label style={labelStyle}>Window timezone</label>
              <input
                className="input"
                placeholder="America/New_York"
                value={form.scheduleTimeZone ?? ''}
                onChange={(e) => setForm({ ...form, scheduleTimeZone: e.target.value })}
              />
              <div style={hintStyle}>
                IANA id. Blank uses the property's own timezone &mdash; which is not necessarily
                where the sender's schedule is anchored. Leave both times empty to poll at one
                rate all day.
              </div>
            </div>
          </div>

          <h3 style={{ fontSize: 13, margin: '24px 0 12px', color: 'var(--text-muted)' }}>
            Summary email
          </h3>
          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, marginBottom: 12 }}>
            <input
              type="checkbox"
              checked={form.sendSummaryEmail}
              onChange={(e) => setForm({ ...form, sendSummaryEmail: e.target.checked })}
            />
            Email the generated summary after each report is ingested
          </label>

          {form.sendSummaryEmail && (
            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 12 }}>
              <div>
                <label style={labelStyle}>Additional recipients</label>
                <textarea
                  className="input"
                  rows={2}
                  placeholder="someone@example.com, another@example.com"
                  value={recipientsText}
                  onChange={(e) => setRecipientsText(e.target.value)}
                />
                <div style={hintStyle}>
                  For people who need the report but have no account. Anyone with an account is
                  set up under Users &mdash; tick <em>Summary email</em> against this property there,
                  so removing their access also stops their mail. Separate with commas,
                  semicolons, or new lines.
                </div>
              </div>
              <div>
                <label style={labelStyle}>Subject prefix</label>
                <input
                  className="input"
                  placeholder="Palm Cove Security Report Summary"
                  value={form.summarySubjectPrefix ?? ''}
                  onChange={(e) => setForm({ ...form, summarySubjectPrefix: e.target.value })}
                />
                <div style={hintStyle}>The report date is appended automatically.</div>
              </div>
            </div>
          )}

          <div style={{ display: 'flex', gap: 8, marginTop: 24, flexWrap: 'wrap' }}>
            <button className="btn btn-primary" onClick={handleSave} disabled={saving}>
              {saving ? 'Saving...' : 'Save configuration'}
            </button>
            <button
              className="btn btn-secondary"
              onClick={handleVerify}
              disabled={!config || busyAction !== null}
            >
              <PlugZap size={16} /> {busyAction === 'verify' ? 'Testing...' : 'Test connection'}
            </button>
            <button
              className="btn btn-secondary"
              onClick={handlePoll}
              disabled={!config || busyAction !== null}
            >
              <RefreshCw size={16} /> {busyAction === 'poll' ? 'Checking...' : 'Check now'}
            </button>
            {config && (
              <button
                className="btn btn-secondary"
                onClick={handleDelete}
                disabled={busyAction !== null}
                style={{ marginLeft: 'auto' }}
              >
                <Trash2 size={16} /> Remove
              </button>
            )}
          </div>

          {pollResult && pollResult.messages.length > 0 && (
            <div className="table-wrapper" style={{ marginTop: 20 }}>
              <table>
                <thead>
                  <tr>
                    <th>Received</th>
                    <th>Subject</th>
                    <th>Attachment</th>
                    <th>Result</th>
                  </tr>
                </thead>
                <tbody>
                  {pollResult.messages.map((m, i) => (
                    <tr key={`${m.receivedAtUtc}-${m.fileName ?? 'none'}-${i}`}>
                      <td style={{ whiteSpace: 'nowrap' }}>{formatTimestamp(m.receivedAtUtc)}</td>
                      <td>{m.subject || '(no subject)'}</td>
                      <td>{m.fileName ?? '-'}</td>
                      <td>
                        {m.error ? (
                          <span style={{ color: 'var(--danger, #dc2626)' }}>{m.error}</span>
                        ) : m.alreadyIngested ? (
                          'Already ingested'
                        ) : (
                          `Ingested${m.summaryEmailSent ? ' · summary emailed' : ''}`
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  );
}
