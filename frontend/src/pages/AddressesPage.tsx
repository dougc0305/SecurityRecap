import { useProperties } from '../hooks/useProperties';

export function AddressesPage() {
  const { properties, selectedPropertyId, setSelectedPropertyId } = useProperties();

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

      <div className="card">
        <div className="empty-state">
          <h3>Coming Soon</h3>
          <p>
            The address watch list will automatically flag addresses with recurring incidents.
            This feature will be populated as patrol reports are ingested.
          </p>
        </div>
      </div>
    </div>
  );
}
