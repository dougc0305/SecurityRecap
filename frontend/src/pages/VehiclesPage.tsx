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
  const pageSize = 20;

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
                    <tr key={v.id}>
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
    </div>
  );
}
