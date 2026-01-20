// Distance calculation utilities
import { Coordinates } from '../types';

/**
 * Calculate the distance between two coordinates using Haversine formula
 */
export function calculateDistance(
  point1: Coordinates,
  point2: Coordinates
): number {
  const R = 6371; // Earth's radius in kilometers
  const dLat = toRadians(point2.lat - point1.lat);
  const dLon = toRadians(point2.lng - point1.lng);

  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(toRadians(point1.lat)) *
      Math.cos(toRadians(point2.lat)) *
      Math.sin(dLon / 2) *
      Math.sin(dLon / 2);

  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return R * c;
}

function toRadians(degrees: number): number {
  return degrees * (Math.PI / 180);
}

/**
 * Sort addresses by distance from a reference point
 */
export function sortByDistance<T extends { coordinates: Coordinates }>(
  items: T[],
  reference: Coordinates
): T[] {
  return [...items].sort((a, b) => {
    const distA = calculateDistance(reference, a.coordinates);
    const distB = calculateDistance(reference, b.coordinates);
    return distA - distB;
  });
}

/**
 * Filter items within a maximum distance
 */
export function filterByMaxDistance<T extends { coordinates: Coordinates }>(
  items: T[],
  reference: Coordinates,
  maxDistanceKm: number
): T[] {
  return items.filter(item => {
    const distance = calculateDistance(reference, item.coordinates);
    return distance <= maxDistanceKm;
  });
}
