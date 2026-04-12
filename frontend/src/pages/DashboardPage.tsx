import { useState, useEffect } from 'react';
import { useProperties } from '../hooks/useProperties';
import { incidentsApi, reportsApi, vehiclesApi } from '../services/api';
import type { Incident } from '../types/api';
import { AlertTriangle, Car, FileText } from 'lucide-react';

export function DashboardPage() {
  const { properties, selectedPropertyId, setSelectedPropertyId, loading: propsLoading } = useProperties();
  const [recentIncidents, setRecentIncidents] = useState<Incident[]>([]);
  const [stats, setStats] = useState({ incidents: 0, reports: 0, vehicles: 0 });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!selectedPropertyId) return;
    setLoading(true);

    Promise.all([
      incidentsApi.getAll({ propertyId: selectedPropertyId, pageSize: 10 }),
      reportsApi.getAll({ propertyId: selectedPropertyId, pageSize: 1 }),
      vehiclesApi.getAll({ propertyId: selectedPropertyId, pageSize: 1 }),
    ])
      .then(([incRes, repRes, vehRes]) => {
        if (incRes.data.success) {
          setRecentIncidents(incRes.data.data);
          setStats((s) => ({ ...s, incidents: incRes.data.totalCount }));
        }
        if (repRes.data.success) setStats((s) => ({ ...s, reports: repRes.data.totalCount }));
        if (vehRes.data.success) setStats((s) => ({ ...s, vehicles: vehRes.data.totalCount }));
      })
      .finally(() => setLoading(false));
  }, [selectedPropertyId]);

  if (propsLoading) return <div className="loading">Loading...</div>;

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Dashboard</h1>
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

      {properties.length === 0 ? (
        <div className="empty-state">
          <h3>No properties yet</h3>
          <p>Add a property in Settings to get started.</p>
        </div>
      ) : (
        <>
          <div className="stats-grid">
            <div className="stat-card">
              <div className="stat-label">Total Incidents</div>
              <div className="stat-value" style={{ color: 'var(--warning)' }}>
                <AlertTriangle size={20} style={{ marginRight: 8, verticalAlign: 'middle' }} />
                {stats.incidents}
              </div>
            </div>
            <div className="stat-card">
              <div className="stat-label">Reports Processed</div>
              <div className="stat-value" style={{ color: 'var(--accent)' }}>
                <FileText size={20} style={{ marginRight: 8, verticalAlign: 'middle' }} />
                {stats.reports}
              </div>
            </div>
            <div className="stat-card">
              <div className="stat-label">Vehicles Tracked</div>
              <div className="stat-value" style={{ color: 'var(--success)' }}>
                <Car size={20} style={{ marginRight: 8, verticalAlign: 'middle' }} />
                {stats.vehicles}
              </div>
            </div>
          </div>

          <div className="card">
            <h2 style={{ fontSize: 16, marginBottom: 16 }}>Recent Incidents</h2>
            {loading ? (
              <div className="loading">Loading...</div>
            ) : recentIncidents.length === 0 ? (
              <div className="empty-state">
                <p>No incidents recorded yet. Upload a patrol report to get started.</p>
              </div>
            ) : (
              <div className="table-wrapper">
                <table>
                  <thead>
                    <tr>
                      <th>Time</th>
                      <th>Type</th>
                      <th>Severity</th>
                      <th>Location</th>
                      <th>Description</th>
                    </tr>
                  </thead>
                  <tbody>
                    {recentIncidents.map((inc) => (
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
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}
