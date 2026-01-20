================================================================================
CODE REVIEW REPORT
================================================================================
Pull Request ID: 4
Project: Joeghanoe
Repository: Joeghanoe
Author: joeghanoe
Title: Declaration - Setup Distancing & Remove Node API
Source Branch: refs/heads/feature/distance
Target Branch: refs/heads/master
Generated: 2026-01-20 17:26:14 UTC
================================================================================
COMMENTS (6 total)
================================================================================

--- Comment 1 ---
File: /portal/src/components/addresses/address-combobox.tsx
Line: 38-50
Category: Performance
Severity: Minor

Message:
The `debounce` function is being recreated on every render due to the empty dependency array in `useMemo`. This could lead to unnecessary re-renders.

Suggestion:
Include `setSearchResults` and `setIsSearching` in the dependency array to ensure the function is stable across renders.

Suggested Code:
```
const debouncedSearch = useMemo(() => debounce(async (query: string) => {
  if (query.length < 2) {
    setSearchResults([]);
    setIsSearching(false);
    return;
  }
  setIsSearching(true);
  const results = await searchAddresses(query);
  setSearchResults(results);
  setIsSearching(false);
}, 300), [setSearchResults, setIsSearching]);
```
--------------------------------------------------------------------------------

--- Comment 2 ---
File: /portal/src/lib/distance.ts
Line: 18-35
Category: ErrorHandling
Severity: Major

Message:
The `calculateDistance` function does not handle network errors or unexpected issues with the fetch call, which could lead to unhandled promise rejections.

Suggestion:
Wrap the fetch call in a try-catch block to handle potential network errors or unexpected issues.

Suggested Code:
```
export async function calculateDistance(from: Coordinates, to: Coordinates): Promise<DistanceResponse> {
  const request: DistanceRequest = { from, to };
  try {
    const response = await fetch(`${API_BASE_URL}/CalculateDistance`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: "Failed to calculate distance" }));
      throw new Error(error.message || "Failed to calculate distance");
    }

    return response.json();
  } catch (error) {
    console.error("Error calculating distance:", error);
    throw new Error("Network error or invalid response");
  }
}
```
--------------------------------------------------------------------------------

--- Comment 3 ---
File: /portal/src/lib/pdok.ts
Line: 38-63
Category: ErrorHandling
Severity: Major

Message:
The `searchAddresses` function does not handle cases where the `centroide_ll` field is missing or malformed, which could lead to runtime errors.

Suggestion:
Add a check to ensure `centroide_ll` exists and is valid before attempting to parse it.

Suggested Code:
```
export async function searchAddresses(query: string): Promise<ParsedAddress[]> {
  if (!query || query.length < 2) {
    return [];
  }

  const params = new URLSearchParams({
    q: query,
    fq: "type:(adres)",
    rows: "5",
  });

  try {
    const response = await fetch(`${PDOK_SUGGEST_URL}?${params}`);
    if (!response.ok) {
      throw new Error("PDOK request failed");
    }

    const data: PDOKResponse = await response.json();
    return data.response.docs.map((doc) => {
      if (!doc.centroide_ll) {
        throw new Error("Missing or invalid centroide_ll field");
      }
      return {
        id: doc.id,
        displayName: doc.weergavenaam,
        coordinates: parseCoordinates(doc.centroide_ll),
      };
    });
  } catch (error) {
    console.error("PDOK search error:", error);
    return [];
  }
}
```
--------------------------------------------------------------------------------

--- Comment 4 ---
File: /portal/src/components/custom/types.ts
Line: 7
Category: Documentation
Severity: Minor

Message:
The `coordinates` property in `SavedAddress` is not documented. It would be helpful to explain its purpose and format.

Suggestion:
Add a comment explaining the purpose of the `coordinates` property and its expected format.

Suggested Code:
```
coordinates: { lat: number; lng: number } // Geographic coordinates (latitude and longitude)
```
--------------------------------------------------------------------------------

--- Comment 5 ---
File: /portal/src/components/statements/statement-row.tsx
Line: 72
Category: ErrorHandling
Severity: Major

Message:
The `calculateDistance` function call does not handle cases where the result might be null or undefined. This could lead to runtime errors.

Suggestion:
Add a check to ensure `result` is valid before accessing its properties.

Suggested Code:
```
if (result && result.distanceKm) { const km = isRoundTrip ? result.distanceKm * 2 : result.distanceKm; setKilometers?.(km); } else { console.error('Invalid distance result:', result); }
```
--------------------------------------------------------------------------------

--- Comment 6 ---
File: /portal/src/components/statements/statement-row.tsx
Line: 243
Category: ErrorHandling
Severity: Major

Message:
The `kilometers` variable is accessed without checking if it is defined. This could cause runtime errors if `kilometers` is null or undefined.

Suggestion:
Add a null or undefined check for `kilometers` before calling `toFixed`.

Suggested Code:
```
{isCalculating ? <Loader2 className="h-3 w-3 animate-spin" /> : <span>{kilometers ? kilometers.toFixed(1) : 'N/A'} km</span>}
```
--------------------------------------------------------------------------------

