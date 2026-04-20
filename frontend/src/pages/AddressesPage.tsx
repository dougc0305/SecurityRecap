import { useEffect, useState } from 'react';
import { useProperties } from '../hooks/useProperties';
import { addressesApi } from '../services/api';
import type { AddressOfInterest, Incident } from '../types/api';

export function AddressesPage() {
  const { properties, selectedPropertyId, setSelectedPropertyId } = useProperties();
  const [addresses, setAddresses] = useState<AddressOfInterest[]>([]);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<{ address: AddressOfInterest; incidents: Incident[] } | null>(null);

  useEffect(() => {
    if (!selectedPropertyId) return;
    setLoading(true);
    addressesApi
      .getAll({ propertyId: selectedPropertyId, page: 1, pageSize: 100 })
      .then((res) => setAddresses(res.data.data))
      .finally(() => setLoading(false));
  }, [selectedPropertyId]);

  const openDetail = async (id: string) => {
    const res = await addressesApi.getById(id);
    if (res.data.success && res.data.data) setSelected(res.data.data);
  };

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Addresses of Interest</h1>
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

      {selected ? (
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h2>{selected.address.address}</h2>
            <button className="btn btn-secondary" onClick={() => setSelected(null)}>← Back</button>
          </div>
          <div style={{ display: 'flex', gap: 24, marginBottom: 16, color: 'var(--text-secondary)' }}>
            <div><strong>Incidents:</strong> {selected.address.incidentCount}</div>
            <div><strong>First flagged:</strong> {selected.address.firstFlagged ? new Date(selected.address.firstFlagged).toLocaleDateString() : '-'}</div>
            <div><strong>Last incident:</strong> {selected.address.lastIncident ? new Date(selected.address.lastIncident).toLocaleString() : '-'}</div>
          </div>
          <h3 style={{ marginTop: 16 }}>Incident History</h3>
          {selected.incidents.length === 0 ? (
            <p style={{ color: 'var(--text-muted)' }}>No incidents recorded.</p>
          ) : (
            <table className="data-table">
              <thead>
                <tr>
                  <th>Time</th>
                  <th>Type</th>
                  <th>Severity</th>
                  <th>Officer</th>
                  <th>Description</th>
                </tr>
              </thead>
              <tbody>
                {selected.incidents.map((i) => (
                  <tr key={i.id}>
                    <td style={{ whiteSpace: 'nowrap' }}>{i.incidentTime ? new Date(i.incidentTime).toLocaleString() : '-'}</td>
                    <td>{i.incidentType}</td>
                    <td>{i.severity}</td>
                    <td>{i.officerName ?? '-'}</td>
                    <td>{i.description}</td>
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
          ) : addresses.length === 0 ? (
            <div className="empty-state">
              <h3>No addresses flagged yet</h3>
              <p>Addresses are automatically flagged as patrol reports are ingested.</p>
            </div>
          ) : (
            <table className="data-table">
              <thead>
                <tr>
                  <th>Address</th>
                  <th>Incidents</th>
                  <th>First Flagged</th>
                  <th>Last Incident</th>
                </tr>
              </thead>
              <tbody>
                {addresses.map((a) => (
                  <tr key={a.id} style={{ cursor: 'pointer' }} onClick={() => openDetail(a.id)}>
                    <td>{a.address}</td>
                    <td>{a.incidentCount}</td>
                    <td style={{ whiteSpace: 'nowrap' }}>
                      {a.firstFlagged ? new Date(a.firstFlagged).toLocaleDateString() : '-'}
                    </td>
                    <td style={{ whiteSpace: 'nowrap' }}>
                      {a.lastIncident ? new Date(a.lastIncident).toLocaleString() : '-'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
}
