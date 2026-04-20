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
        }
      })
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (properties.length === 0) {
      setSelectedPropertyId('');
      localStorage.removeItem('selectedPropertyId');
      return;
    }

    const hasSelectedProperty = properties.some((property) => property.id === selectedPropertyId);
    if (!selectedPropertyId || !hasSelectedProperty) {
      setSelectedPropertyId(properties[0].id);
    }
  }, [properties, selectedPropertyId]);

  useEffect(() => {
    if (selectedPropertyId) {
      localStorage.setItem('selectedPropertyId', selectedPropertyId);
    }
  }, [selectedPropertyId]);

  const selectedProperty = properties.find((p) => p.id === selectedPropertyId) || null;

  return { properties, loading, selectedPropertyId, setSelectedPropertyId, selectedProperty };
}
