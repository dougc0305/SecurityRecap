import { useState, useEffect } from 'react';
import DOMPurify from 'dompurify';
import { useProperties } from '../hooks/useProperties';
import { useAuth } from '../hooks/useAuth';
import { reportsApi, ingestApi } from '../services/api';
import type { Report } from '../types/api';
import { Upload, ChevronLeft, ChevronRight, FileText, Eye } from 'lucide-react';
import { canIngestReports } from '../utils/authorization';

export function ReportsPage() {
  const { user } = useAuth();
  const { properties, selectedPropertyId, setSelectedPropertyId } = useProperties();
  const [reports, setReports] = useState<Report[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [selectedReport, setSelectedReport] = useState<Report | null>(null);
  const pageSize = 20;
  const canUploadReports = canIngestReports(user);
  const sanitizedSummaryHtml = selectedReport?.aiSummaryHtml
    ? DOMPurify.sanitize(selectedReport.aiSummaryHtml, { USE_PROFILES: { html: true } })
    : '';

  const fetchReports = () => {
    if (!selectedPropertyId) return;
    setLoading(true);
    reportsApi
      .getAll({ propertyId: selectedPropertyId, page, pageSize })
      .then((res) => {
        if (res.data.success) {
          setReports(res.data.data);
          setTotalCount(res.data.totalCount);
        }
      })
      .finally(() => setLoading(false));
  };

  useEffect(fetchReports, [selectedPropertyId, page]);

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file || !selectedPropertyId) return;
    setUploading(true);
    try {
      await ingestApi.uploadReport(selectedPropertyId, file);
      fetchReports();
    } catch (err: unknown) {
      const e = err as {
        response?: {
          data?: {
            error?: string;
            title?: string;
            detail?: string;
            errors?: Record<string, string[]>;
          };
        };
        message?: string;
      };
      const data = e.response?.data;
      const validationErrors = data?.errors
        ? Object.values(data.errors).flat().join(' ')
        : undefined;
      const reason = data?.error
        ?? validationErrors
        ?? data?.detail
        ?? data?.title
        ?? e.message
        ?? 'Unknown error.';
      alert(`Upload failed: ${reason}`);
      console.error(err);
    } finally {
      setUploading(false);
      e.target.value = '';
    }
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Reports</h1>
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          <select
            className="select"
            value={selectedPropertyId}
            onChange={(e) => { setSelectedPropertyId(e.target.value); setPage(1); }}
          >
            {properties.map((p) => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </select>
          {canUploadReports && (
            <label className="btn btn-primary" style={{ position: 'relative' }}>
              <Upload size={16} />
              {uploading ? 'Processing...' : 'Upload PDF'}
              <input
                type="file"
                accept=".pdf"
                onChange={handleUpload}
                disabled={uploading || !selectedPropertyId}
                style={{ position: 'absolute', opacity: 0, width: '100%', height: '100%', left: 0, top: 0, cursor: 'pointer' }}
              />
            </label>
          )}
        </div>
      </div>

      {selectedReport ? (
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h2 style={{ fontSize: 16 }}>
              Report - {selectedReport.reportDate}
            </h2>
            <button className="btn btn-secondary" onClick={() => setSelectedReport(null)}>
              Back to list
            </button>
          </div>
          {selectedReport.officerNames.length > 0 && (
            <p style={{ fontSize: 13, color: 'var(--text-muted)', marginBottom: 16 }}>
              Officers: {selectedReport.officerNames.join(', ')}
            </p>
          )}
          {selectedReport.aiSummaryHtml ? (
            <div
              style={{ lineHeight: 1.7, color: 'var(--text-secondary)' }}
              dangerouslySetInnerHTML={{ __html: sanitizedSummaryHtml }}
            />
          ) : (
            <p style={{ color: 'var(--text-muted)' }}>No AI summary available.</p>
          )}
          {selectedReport.rawPdfUrl && (
            <div style={{ marginTop: 16 }}>
              <button
                type="button"
                className="btn btn-secondary"
                onClick={async () => {
                  const res = await reportsApi.getPdfBlob(selectedReport.id);
                  const url = URL.createObjectURL(res.data);
                  window.open(url, '_blank', 'noopener,noreferrer');
                  setTimeout(() => URL.revokeObjectURL(url), 60_000);
                }}
              >
                <FileText size={16} /> Download Original PDF
              </button>
            </div>
          )}
        </div>
      ) : (
        <div className="card">
          {loading ? (
            <div className="loading">Loading...</div>
          ) : reports.length === 0 ? (
            <div className="empty-state">
              <h3>No reports yet</h3>
              <p>Upload a patrol report PDF to get started.</p>
            </div>
          ) : (
            <>
              <div className="table-wrapper">
                <table>
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Officers</th>
                      <th>Summary</th>
                      <th>PDF</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {reports.map((r) => (
                      <tr key={r.id}>
                        <td style={{ whiteSpace: 'nowrap' }}>{r.reportDate}</td>
                        <td>{r.officerNames.join(', ') || '-'}</td>
                        <td>{r.aiSummaryHtml ? 'Available' : '-'}</td>
                        <td>
                          {r.rawPdfUrl ? (
                            <button
                              type="button"
                              className="btn btn-secondary"
                              style={{ padding: '4px 10px' }}
                              onClick={async () => {
                                const res = await reportsApi.getPdfBlob(r.id);
                                const url = URL.createObjectURL(res.data);
                                window.open(url, '_blank', 'noopener,noreferrer');
                                setTimeout(() => URL.revokeObjectURL(url), 60_000);
                              }}
                            >
                              <FileText size={16} />
                            </button>
                          ) : '-'}
                        </td>
                        <td>
                          <button
                            className="btn btn-secondary"
                            style={{ padding: '4px 10px', fontSize: 12 }}
                            onClick={() => {
                              reportsApi.getById(r.id).then((res) => {
                                if (res.data.success && res.data.data) setSelectedReport(res.data.data);
                              });
                            }}
                          >
                            <Eye size={14} /> View
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {totalPages > 1 && (
                <div className="pagination">
                  <button className="btn btn-secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                    <ChevronLeft size={16} />
                  </button>
                  <span className="pagination-info">Page {page} of {totalPages}</span>
                  <button className="btn btn-secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                    <ChevronRight size={16} />
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}
