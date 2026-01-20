import { useState, useEffect, useMemo } from 'react';
import { Address, Coordinates } from '../types';
import { fetchAddresses } from '../api/addresses';
import { sortByDistance, filterByMaxDistance } from '../lib/distance';

export interface UseAddressesOptions {
  sortByDistanceFrom?: Coordinates;
  maxDistanceKm?: number;
}

export function useAddresses(userId: string, options?: UseAddressesOptions) {
  const [addresses, setAddresses] = useState<Address[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    async function loadAddresses() {
      try {
        setLoading(true);
        const data = await fetchAddresses(userId);
        setAddresses(data);
      } catch (err) {
        setError(err as Error);
      } finally {
        setLoading(false);
      }
    }

    loadAddresses();
  }, [userId]);

  const processedAddresses = useMemo(() => {
    let result = addresses;

    if (options?.maxDistanceKm && options?.sortByDistanceFrom) {
      result = filterByMaxDistance(result, options.sortByDistanceFrom, options.maxDistanceKm);
    }

    if (options?.sortByDistanceFrom) {
      result = sortByDistance(result, options.sortByDistanceFrom);
    }

    return result;
  }, [addresses, options?.sortByDistanceFrom, options?.maxDistanceKm]);

  return {
    addresses: processedAddresses,
    allAddresses: addresses,
    loading,
    error
  };
}
