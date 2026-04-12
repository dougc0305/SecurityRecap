import { useState, useEffect } from 'react';
import { useProperties } from '../hooks/useProperties';
import { incidentsApi } from '../services/api';
import type { Incident } from '../types/api';
import { ChevronLeft, ChevronRight } from 'lucide-react';

const INCIDENT_TYPES = ['', 'Noise', 'Parking', 'Maintenance', 'Gate', 'LawEnforcement', 'Patrol', 'PhoneCall'];
const SEVERITIES = ['', 'Low', 'Medium', 'High', 'Urgent'];

export function IncidentsPage() {
  const { properties, selectedPropertyId, setSelectedPropertyId } = useProperties();
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [typeFilter, setTypeFilter] = useState('');
  const [severityFilter, setSeverityFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const pageSize = 20;

  useEffect(() => {
    if (!selectedPropertyId) return;
    setLoading(true);
    incidentsApi
      .getAll({
        propertyId: selectedPropertyId,
        type: typeFilter || undefined,
        severity: severityFilter || undefined,
        page,
        pageSize,
      })
      .then((res) => {
        if (res.data.success) {
          setIncidents(res.data.data);
          setTotalCount(res.data.totalCount);
        }
      })
      .finally(() => setLoading(false));
  }, [selectedPropertyId, typeFilter, severityFilter, page]);

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Incidents</h1>
        <select
          className="select"
          value={selectedPropertyId}
          onChange={(e) => { setSelectedPropertyId(e.target.value); setPage(1); }}
        >
          {properties.map((p) => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>
      </div>

      <div className="filters">
        <select
          className="select"
          value={typeFilter}
          onChange={(e) => { setTypeFilter(e.target.value); setPage(1); }}
        >
          <option value="">All Types</option>
          {INCIDENT_TYPES.filter(Boolean).map((t) => (
            <option key={t} value={t}>{t}</option>
          ))}
        </select>
        <select
          className="select"
          value={severityFilter}
          onChange={(e) => { setSeverityFilter(e.target.value); setPage(1); }}
        >
          <option value="">All Severities</option>
          {SEVERITIES.filter(Boolean).map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
        <span className="pagination-info">{totalCount} incident{totalCount !== 1 ? 's' : ''}</span>
      </div>

      <div className="card">
        {loading ? (
          <div className="loading">Loading...</div>
        ) : incidents.length === 0 ? (
          <div className="empty-state">
            <h3>No incidents found</h3>
            <p>Try adjusting your filters or upload a patrol report.</p>
          </div>
        ) : (
          <>
            <div className="table-wrapper">
              <table>
                <thead>
                  <tr>
                    <th>Time</th>
                    <th>Type</th>
                    <th>Severity</th>
                    <th>Location</th>
                    <th>Description</th>
                    <th>Officer</th>
                    <th>LE</th>
                  </tr>
                </thead>
                <tbody>
                  {incidents.map((inc) => (
                    <tr key={inc.id}>
                      <td style={{ whiteSpace: 'nowrap' }}>
                        {inc.incidentTime
                          ? new Date(inc.incidentTime).toLocaleString()
                          : '-'}
                      </td>
                      <td>
                        <span className={`badge badge-${inc.incidentType.toLowerCase()}`}>
                          {inc.incidentType}
                        </span>
                      </td>
                      <td>
                        <span className={`badge badge-${inc.severity.toLowerCase()}`}>
                          {inc.severity}
                        </span>
                      </td>
                      <td>{inc.location || '-'}</td>
                      <td style={{ maxWidth: 300, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {inc.description}
                      </td>
                      <td>{inc.officerName || '-'}</td>
                      <td>{inc.lawEnforcement ? 'Yes' : '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {totalPages > 1 && (
              <div className="pagination">
                <button
                  className="btn btn-secondary"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => p - 1)}
                >
                  <ChevronLeft size={16} />
                </button>
                <span className="pagination-info">Page {page} of {totalPages}</span>
                <button
                  className="btn btn-secondary"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                >
                  <ChevronRight size={16} />
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
