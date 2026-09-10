import { useMemo, useState } from 'react';
import { FileDown, Printer, Sparkles } from 'lucide-react';
import { useProperties } from '../hooks/useProperties';
import { recapApi } from '../services/api';
import type { RecapResult } from '../types/api';

/** Returns yyyy-MM-dd for a Date, in local time rather than UTC. */
function isoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function lastNDays(n: number): { from: string; to: string } {
  const to = new Date();
  const from = new Date();
  from.setDate(from.getDate() - (n - 1));
  return { from: isoDate(from), to: isoDate(to) };
}

/** The calendar month before the current one — the usual span for a monthly board meeting. */
function lastFullMonth(): { from: string; to: string } {
  const now = new Date();
  const first = new Date(now.getFullYear(), now.getMonth() - 1, 1);
  const last = new Date(now.getFullYear(), now.getMonth(), 0);
  return { from: isoDate(first), to: isoDate(last) };
}

function formatDate(value: string | null): string {
  if (!value) return '-';
  const d = new Date(value.length <= 10 ? `${value}T00:00:00` : value);
  return Number.isNaN(d.getTime()) ? value : d.toLocaleDateString();
}

function formatDateTime(value: string | null): string {
  if (!value) return '-';
  const d = new Date(value);
  return Number.isNaN(d.getTime())
    ? value
    : d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}

const labelStyle: React.CSSProperties = {
  display: 'block', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4,
};

export function RecapPage() {
  const { properties, selectedPropertyId, setSelectedPropertyId } = useProperties();
  const initial = lastNDays(30);
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<RecapResult | null>(null);

  const propertyName = useMemo(
    () => properties.find((p) => p.id === selectedPropertyId)?.name ?? 'property',
    [properties, selectedPropertyId]
  );

  const generate = async () => {
    if (!selectedPropertyId) return;
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const res = await recapApi.get({ propertyId: selectedPropertyId, from, to });
      if (res.data.success && res.data.data) setResult(res.data.data);
      else setError(res.data.error ?? 'Could not build the recap.');
    } catch (err: unknown) {
      const e = err as { response?: { data?: { error?: string } }; message?: string };
      setError(e.response?.data?.error ?? e.message ?? 'Could not build the recap.');
    } finally {
      setLoading(false);
    }
  };

  const downloadMarkdown = () => {
    if (!result?.narrative) return;
    const blob = new Blob([result.narrative.markdownSummary], { type: 'text/markdown' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `security-recap-${from}-to-${to}.md`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 60_000);
  };

  const applyPreset = (preset: { from: string; to: string }) => {
    setFrom(preset.from);
    setTo(preset.to);
  };

  const data = result?.data;
  const narrative = result?.narrative;

  return (
    <div>
      <div className="page-header no-print">
        <div>
          <h1 className="page-title">Board Recap</h1>
          <p style={{ fontSize: 13, color: 'var(--text-muted)', margin: '4px 0 0' }}>
            A period summary for the meeting packet. Counts come from the database; the reading
            of them comes from the AI.
          </p>
        </div>
      </div>

      <div className="card no-print" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <div style={{ minWidth: 190 }}>
            <label style={labelStyle}>Property</label>
            <select
              className="select"
              style={{ width: '100%' }}
              value={selectedPropertyId}
              onChange={(e) => setSelectedPropertyId(e.target.value)}
            >
              {properties.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
          </div>
          <div>
            <label style={labelStyle}>From</label>
            <input className="input" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>To</label>
            <input className="input" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </div>
          <button className="btn btn-primary" onClick={generate} disabled={loading || !selectedPropertyId}>
            <Sparkles size={16} /> {loading ? 'Building recap...' : 'Generate recap'}
          </button>
        </div>

        <div style={{ display: 'flex', gap: 8, marginTop: 12, flexWrap: 'wrap' }}>
          <span style={{ fontSize: 12, color: 'var(--text-muted)', alignSelf: 'center' }}>Quick ranges:</span>
          <button className="btn btn-secondary" style={{ fontSize: 12, padding: '4px 10px' }}
            onClick={() => applyPreset(lastFullMonth())}>Last full month</button>
          <button className="btn btn-secondary" style={{ fontSize: 12, padding: '4px 10px' }}
            onClick={() => applyPreset(lastNDays(30))}>Last 30 days</button>
          <button className="btn btn-secondary" style={{ fontSize: 12, padding: '4px 10px' }}
            onClick={() => applyPreset(lastNDays(90))}>Last 90 days</button>
        </div>

        {loading && (
          <p style={{ fontSize: 13, color: 'var(--text-muted)', marginTop: 14, marginBottom: 0 }}>
            Reading every report in the period. This usually takes under a minute.
          </p>
        )}
        {error && (
          <div style={{ background: '#fef2f2', color: '#991b1b', padding: 10, borderRadius: 6, marginTop: 14, fontSize: 13 }}>
            {error}
          </div>
        )}
      </div>

      {data && (
        <>
          <div className="card" style={{ marginBottom: 24 }}>
            <div className="no-print" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, marginBottom: 16, flexWrap: 'wrap' }}>
              <div>
                <h2 style={{ fontSize: 17, margin: 0 }}>{data.propertyName} &middot; {formatDate(data.coverage.from)} to {formatDate(data.coverage.to)}</h2>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                {narrative && (
                  <button className="btn btn-secondary" onClick={downloadMarkdown}>
                    <FileDown size={16} /> Download .md
                  </button>
                )}
                <button className="btn btn-secondary" onClick={() => window.print()}>
                  <Printer size={16} /> Print
                </button>
              </div>
            </div>

            {result?.narrativeError && (
              <div style={{ background: '#fffbeb', color: '#92400e', padding: 10, borderRadius: 6, marginBottom: 16, fontSize: 13 }}>
                The written summary could not be generated: {result.narrativeError} The figures below are unaffected.
              </div>
            )}

            {narrative && (
              <p style={{ fontSize: 16, lineHeight: 1.6, marginTop: 0 }}>{narrative.headline}</p>
            )}

            <div style={{
              display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
              gap: 12, marginTop: 16,
            }}>
              <Stat label="Nights covered" value={`${data.coverage.usableReports} of ${data.coverage.nightsInPeriod}`} />
              <Stat label="Substantive entries" value={data.substantiveEntries} />
              <Stat label="Routine entries" value={data.routineEntries} />
              <Stat label="Parking violations" value={data.violations.length} />
              <Stat label="Officers" value={data.coverage.officers.length} />
            </div>

            {(data.coverage.missingDates.length > 0 || data.coverage.emptyReportDates.length > 0) && (
              <p style={{ fontSize: 13, color: 'var(--text-muted)', marginTop: 14, marginBottom: 0 }}>
                {data.coverage.missingDates.length > 0 && (
                  <>No report received for {data.coverage.missingDates.map(formatDate).join(', ')}. </>
                )}
                {data.coverage.emptyReportDates.length > 0 && (
                  <>{data.coverage.emptyReportDates.map(formatDate).join(', ')} arrived but stored no entries.
                    {' '}Totals below are a floor, not the whole period.</>
                )}
              </p>
            )}
          </div>

          {narrative && narrative.attentionItems.length > 0 && (
            <div className="card" style={{ marginBottom: 24 }}>
              <h2 style={{ fontSize: 16, marginBottom: 4 }}>For board attention</h2>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 14, marginTop: 14 }}>
                {narrative.attentionItems.map((item, i) => (
                  <div key={i} style={{
                    borderLeft: `3px solid ${significanceColor(item.significance)}`,
                    paddingLeft: 14,
                  }}>
                    <div style={{ display: 'flex', gap: 8, alignItems: 'baseline', flexWrap: 'wrap' }}>
                      <strong style={{ fontSize: 15 }}>{item.title}</strong>
                      <span className={`badge ${significanceBadge(item.significance)}`}>
                        {significanceLabel(item.significance)}
                      </span>
                      {item.when && (
                        <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{item.when}</span>
                      )}
                    </div>
                    <p style={{ margin: '6px 0 6px', lineHeight: 1.6 }}>{item.what}</p>
                    <p style={{ margin: 0, fontSize: 13.5, color: 'var(--text-muted)' }}>
                      <strong style={{ color: 'var(--text-secondary)' }}>Why it matters:</strong> {item.whyItMatters}
                    </p>
                  </div>
                ))}
              </div>
            </div>
          )}

          <div className="card" style={{ marginBottom: 24 }}>
            <h2 style={{ fontSize: 16, marginBottom: 4 }}>Activity</h2>
            <p style={{ fontSize: 13, color: 'var(--text-muted)', marginTop: 0 }}>
              {data.totalEntries} entries logged. {data.routineEntries} are routine patrol rounds
              and scheduled gate locks, recorded whether or not anything happened.
            </p>
            <div className="table-wrapper">
              <table>
                <thead>
                  <tr><th>Category</th><th>High</th><th>Medium</th><th>Low</th><th>Total</th></tr>
                </thead>
                <tbody>
                  {data.byCategory.map((c) => (
                    <tr key={c.category}>
                      <td>{c.category}</td>
                      <td>{c.high || '-'}</td>
                      <td>{c.medium || '-'}</td>
                      <td>{c.low || '-'}</td>
                      <td style={{ fontWeight: 600 }}>{c.total}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {data.violations.length > 0 && (
            <div className="card" style={{ marginBottom: 24 }}>
              <h2 style={{ fontSize: 16, marginBottom: 12 }}>Parking violations</h2>
              <div className="table-wrapper">
                <table>
                  <thead>
                    <tr>
                      <th>When</th><th>Plate</th><th>Vehicle</th><th>Location</th>
                      <th>Violation</th><th>Action</th><th>History</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.violations.map((v, i) => (
                      <tr key={i}>
                        <td style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v.localTime)}</td>
                        <td style={{ whiteSpace: 'nowrap', fontWeight: 600 }}>
                          {v.plateNumber ? `${v.plateState ?? ''} ${v.plateNumber}`.trim() : 'No plate'}
                        </td>
                        <td>{v.vehicle ?? '-'}</td>
                        <td>{v.location ?? '-'}</td>
                        <td>{v.violationType ?? '-'}</td>
                        <td style={{ whiteSpace: 'nowrap' }}>
                          {[v.towNotified && 'Tow', v.noticeIssued && 'Notice'].filter(Boolean).join(' + ') || '-'}
                        </td>
                        <td style={{ whiteSpace: 'nowrap' }}>
                          {v.plateNumber && v.plateViolationsAllTime > 1 ? (
                            <span className="badge badge-medium">
                              {v.plateViolationsAllTime} all time
                            </span>
                          ) : v.plateNumber ? 'First' : '-'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {data.recurringMaintenance.length > 0 && (
            <div className="card" style={{ marginBottom: 24 }}>
              <h2 style={{ fontSize: 16, marginBottom: 4 }}>Recurring items</h2>
              <p style={{ fontSize: 13, color: 'var(--text-muted)', marginTop: 0 }}>
                Reported more than once in the period without a logged resolution.
              </p>
              <div className="table-wrapper">
                <table>
                  <thead><tr><th>Item</th><th>Location</th><th>Times</th><th>First</th><th>Last</th></tr></thead>
                  <tbody>
                    {data.recurringMaintenance.map((m, i) => (
                      <tr key={i}>
                        <td>{m.description}</td>
                        <td>{m.location ?? '-'}</td>
                        <td style={{ fontWeight: 600 }}>{m.occurrences}</td>
                        <td style={{ whiteSpace: 'nowrap' }}>{formatDate(m.first)}</td>
                        <td style={{ whiteSpace: 'nowrap' }}>{formatDate(m.last)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {data.highSeverity.length > 0 && (
            <div className="card" style={{ marginBottom: 24 }}>
              <h2 style={{ fontSize: 16, marginBottom: 12 }}>High-severity entries, as recorded</h2>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                {data.highSeverity.map((e, i) => (
                  <div key={i} style={{ borderLeft: '2px solid var(--border)', paddingLeft: 14 }}>
                    <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>
                      {formatDateTime(e.localTime)} &middot; {e.incidentType}
                      {e.location ? ` · ${e.location}` : ''}
                    </div>
                    <p style={{ margin: '4px 0 0', lineHeight: 1.6 }}>{e.description}</p>
                  </div>
                ))}
              </div>
            </div>
          )}

          {narrative && narrative.dataNotes.length > 0 && (
            <div className="card">
              <h2 style={{ fontSize: 16, marginBottom: 12 }}>Notes on the record</h2>
              <ul style={{ margin: 0, paddingLeft: 20, color: 'var(--text-muted)', fontSize: 13.5, lineHeight: 1.7 }}>
                {narrative.dataNotes.map((n, i) => <li key={i}>{n}</li>)}
              </ul>
            </div>
          )}
        </>
      )}

      {!data && !loading && (
        <div className="card">
          <div className="empty-state">
            <h3>No recap yet</h3>
            <p>Pick a period and generate a recap for {propertyName}.</p>
          </div>
        </div>
      )}
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string | number }) {
  return (
    <div style={{
      background: 'var(--bg-primary)', border: '1px solid var(--border)',
      borderRadius: 'var(--radius)', padding: '10px 14px',
    }}>
      <div style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-muted)' }}>
        {label}
      </div>
      <div style={{ fontSize: 20, fontWeight: 600, marginTop: 2 }}>{value}</div>
    </div>
  );
}

function significanceColor(s: string): string {
  if (s === 'act_now') return '#dc2626';
  if (s === 'discuss') return '#d97706';
  return 'var(--border)';
}
function significanceBadge(s: string): string {
  if (s === 'act_now') return 'badge-high';
  if (s === 'discuss') return 'badge-medium';
  return 'badge-low';
}
function significanceLabel(s: string): string {
  if (s === 'act_now') return 'Act now';
  if (s === 'discuss') return 'Discuss';
  return 'Monitor';
}
