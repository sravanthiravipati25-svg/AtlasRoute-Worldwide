const BASE_URL =
  import.meta.env.VITE_API_BASE_URL ||
  "https://atlasroute-worldwide-1.onrender.com/api";

async function request(path, options = {}) {
  const res = await fetch(`${BASE_URL}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  })

  const data = await res.json().catch(() => null)

  if (!res.ok) {
    const message = data?.message || `Request failed (${res.status})`
    throw new Error(message)
  }

  return data
}

export const api = {
  getLocations: () => request('/locations'),
  getGraph: () => request('/routes/graph'),

  optimizeRoute: (sourceLocationId, destinationLocationId, priority) =>
    request('/routes/optimize', {
      method: 'POST',
      body: JSON.stringify({ sourceLocationId, destinationLocationId, priority }),
    }),

  astarRoute: (sourceLocationId, destinationLocationId) =>
    request('/routes/astar', {
      method: 'POST',
      body: JSON.stringify({ sourceLocationId, destinationLocationId }),
    }),

  reachable: (sourceId, maxHops) =>
    request(`/routes/reachable/${sourceId}?maxHops=${maxHops}`),

  worldSearch: (query, signal) =>
    request(`/world/search?q=${encodeURIComponent(query)}`, { signal }),

  worldRoute: (source, destination, profile = 'driving') =>
    request('/world/route', {
      method: 'POST',
      body: JSON.stringify({
        sourceLatitude: source.latitude,
        sourceLongitude: source.longitude,
        destinationLatitude: destination.latitude,
        destinationLongitude: destination.longitude,
        profile,
      }),
    }),

  compareWorldModes: async (source, destination) => {
    const profiles = ['driving', 'cycling', 'walking']
    const results = await Promise.all(
      profiles.map(async (profile) => {
        try {
          const route = await api.worldRoute(source, destination, profile)
          return { profile, route, error: null }
        } catch (error) {
          return { profile, route: null, error: error.message }
        }
      }),
    )
    return results
  },

  buildWorldGraph: (nodes, profile = 'driving') =>
    request('/world/graph/build', {
      method: 'POST',
      body: JSON.stringify({ nodes, profile }),
    }),

  routeOnWorldGraph: (nodes, sourceId, destinationId, priority, useAStar, closedEdgeKeys, profile = 'driving') =>
    request('/world/graph/route', {
      method: 'POST',
      body: JSON.stringify({
        nodes,
        sourceId,
        destinationId,
        priority,
        useAStar,
        closedEdgeKeys,
        profile,
      }),
    }),

  compareWorldGraph: (nodes, sourceId, destinationId, closedEdgeKeys, profile = 'driving') =>
    request('/world/graph/compare', {
      method: 'POST',
      body: JSON.stringify({ nodes, sourceId, destinationId, closedEdgeKeys, profile }),
    }),
}
