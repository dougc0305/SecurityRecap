import { useState, useEffect } from 'react';
import { useProperties } from '../hooks/useProperties';
import { vehiclesApi } from '../services/api';
import type { Vehicle } from '../types/api';
import { Search, ChevronLeft, ChevronRight } from 'lucide-react';

export function VehiclesPage() {
  const { properties, selectedPropertyId, setSelectedPropertyId } = useProperties();
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [plateSearch, setPlateSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<Vehicle | null>(null);
  const pageSize = 20;

  const openDetail = async (id: string) => {
    const res = await vehiclesApi.getById(id);
    if (res.data.success && res.data.data) setSelected(res.data.data);
  };

  useEffect(() => {
    if (!selectedPropertyId) return;
    setLoading(true);
    vehiclesApi
      .getAll({
        propertyId: selectedPropertyId,
        plate: plateSearch || undefined,
        page,
        pageSize,
      })
      .then((res) => {
        if (res.data.success) {
          setVehicles(res.data.data);
          setTotalCount(res.data.totalCount);
        }
      })
      .finally(() => setLoading(false));
  }, [selectedPropertyId, plateSearch, page]);

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Vehicles</h1>
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
        <div style={{ position: 'relative', width: 260 }}>
          <Search size={16} style={{ position: 'absolute', left: 10, top: 10, color: 'var(--text-muted)' }} />
          <input
            className="input"
            style={{ paddingLeft: 32 }}
            placeholder="Search by plate number..."
            value={plateSearch}
            onChange={(e) => { setPlateSearch(e.target.value); setPage(1); }}
          />
        </div>
        <span className="pagination-info">{totalCount} vehicle{totalCount !== 1 ? 's' : ''}</span>
      </div>

      {selected ? (
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h2 style={{ fontFamily: 'monospace', letterSpacing: 1 }}>{selected.plateNumber}</h2>
            <button className="btn btn-secondary" onClick={() => setSelected(null)}>← Back</button>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12, marginBottom: 16, color: 'var(--text-secondary)' }}>
            <div><strong>State:</strong> {selected.plateState ?? '-'}</div>
            <div><strong>Make:</strong> {selected.make ?? '-'}</div>
            <div><strong>Model:</strong> {selected.model ?? '-'}</div>
            <div><strong>Color:</strong> {selected.color ?? '-'}</div>
            <div><strong>Violations:</strong> {selected.violationCount}</div>
            <div><strong>First seen:</strong> {selected.firstSeen ? new Date(selected.firstSeen).toLocaleDateString() : '-'}</div>
            <div><strong>Last seen:</strong> {selected.lastSeen ? new Date(selected.lastSeen).toLocaleDateString() : '-'}</div>
          </div>
          <h3 style={{ marginTop: 16 }}>Violation History</h3>
          {!selected.violations || selected.violations.length === 0 ? (
            <p style={{ color: 'var(--text-muted)' }}>No violations recorded.</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Type</th>
                  <th>Location</th>
                  <th>Notice</th>
                  <th>Tow</th>
                  <th>Date</th>
                </tr>
              </thead>
              <tbody>
                {selected.violations.map((v) => (
                  <tr key={v.id}>
                    <td>{v.violationType}</td>
                    <td>{v.location ?? '-'}</td>
                    <td>{v.noticeIssued ? 'Yes' : 'No'}</td>
                    <td>{v.towNotified ? 'Yes' : 'No'}</td>
                    <td style={{ whiteSpace: 'nowrap' }}>{new Date(v.createdAt).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      ) : (
      <div className="card">
        {loading ? (
          <div className="loading">Loading...</div>
        ) : vehicles.length === 0 ? (
          <div className="empty-state">
            <h3>No vehicles found</h3>
            <p>Vehicle data is populated from patrol report ingestion.</p>
          </div>
        ) : (
          <>
            <div className="table-wrapper">
              <table>
                <thead>
                  <tr>
                    <th>Plate</th>
                    <th>State</th>
                    <th>Make</th>
                    <th>Model</th>
                    <th>Color</th>
                    <th>Violations</th>
                    <th>First Seen</th>
                    <th>Last Seen</th>
                  </tr>
                </thead>
                <tbody>
                  {vehicles.map((v) => (
                    <tr key={v.id} style={{ cursor: 'pointer' }} onClick={() => openDetail(v.id)}>
                      <td style={{ fontWeight: 600, fontFamily: 'monospace', letterSpacing: 1 }}>
                        {v.plateNumber}
                      </td>
                      <td>{v.plateState || '-'}</td>
                      <td>{v.make || '-'}</td>
                      <td>{v.model || '-'}</td>
                      <td>{v.color || '-'}</td>
                      <td>
                        <span className={`badge ${v.violationCount >= 3 ? 'badge-high' : v.violationCount >= 2 ? 'badge-medium' : 'badge-low'}`}>
                          {v.violationCount}
                        </span>
                      </td>
                      <td>{v.firstSeen ? new Date(v.firstSeen).toLocaleDateString() : '-'}</td>
                      <td>{v.lastSeen ? new Date(v.lastSeen).toLocaleDateString() : '-'}</td>
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
      )}
    </div>
  );
}
