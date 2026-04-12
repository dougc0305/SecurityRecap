import { useState, useEffect } from 'react';
import { propertiesApi } from '../services/api';
import type { Property } from '../types/api';

export function useProperties() {
  const [properties, setProperties] = useState<Property[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedPropertyId, setSelectedPropertyId] = useState<string>(() => {
    return localStorage.getItem('selectedPropertyId') || '';
  });

  useEffect(() => {
    propertiesApi.getAll()
      .then((res) => {
        if (res.data.success && res.data.data) {
          setProperties(res.data.data);
          if (!selectedPropertyId && res.data.data.length > 0) {
            setSelectedPropertyId(res.data.data[0].id);
          }
        }
      })
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (selectedPropertyId) {
      localStorage.setItem('selectedPropertyId', selectedPropertyId);
    }
  }, [selectedPropertyId]);

  const selectedProperty = properties.find((p) => p.id === selectedPropertyId) || null;

  return { properties, loading, selectedPropertyId, setSelectedPropertyId, selectedProperty };
}
