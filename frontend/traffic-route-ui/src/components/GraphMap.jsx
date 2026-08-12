import { useEffect } from 'react'
import {
  CircleMarker,
  MapContainer,
  Polyline,
  TileLayer,
  Tooltip,
  useMap,
} from 'react-leaflet'
import 'leaflet/dist/leaflet.css'

const CONGESTION_COLOR = {
  Low: '#3a6f63',
  Medium: '#8492A6',
  High: '#e0a13a',
  Severe: '#e5484d',
}

function FitAll({ nodes, routeGeometry }) {
  const map = useMap()

  useEffect(() => {
    const points = nodes.map((n) => [n.latitude, n.longitude])
    if (routeGeometry?.length) {
      routeGeometry.forEach((p) => points.push([p.latitude, p.longitude]))
    }
    if (points.length >= 2) {
      map.fitBounds(points, { padding: [60, 60], maxZoom: 13 })
    } else if (points.length === 1) {
      map.flyTo(points[0], 10, { duration: 0.8 })
    }
  }, [map, nodes, routeGeometry])

  return null
}

export default function GraphMap({ nodes, edges, sourceId, destinationId, routeGeometry }) {
  const seenPairs = new Set()
  const uniqueEdges = edges.filter((e) => {
    const key = [e.fromId, e.toId].sort().join('|')
    if (seenPairs.has(key)) return false
    seenPairs.add(key)
    return true
  })

  const nodeById = Object.fromEntries(nodes.map((n) => [n.id, n]))

  return (
    <div className="world-map-shell">
      <MapContainer center={[20, 0]} zoom={2.4} minZoom={2} maxZoom={18} worldCopyJump scrollWheelZoom className="world-map">
        <TileLayer attribution='&copy; OpenStreetMap contributors' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
        <FitAll nodes={nodes} routeGeometry={routeGeometry} />

        {uniqueEdges.map((e) => {
          const a = nodeById[e.fromId]
          const b = nodeById[e.toId]
          if (!a || !b) return null
          return (
            <Polyline
              key={`${e.fromId}-${e.toId}`}
              positions={[[a.latitude, a.longitude], [b.latitude, b.longitude]]}
              pathOptions={{
                color: e.isClosed ? '#e5484d' : CONGESTION_COLOR[e.congestion] || '#3a6f63',
                weight: 3,
                opacity: e.isClosed ? 0.85 : 0.55,
                dashArray: e.isClosed ? '6 6' : undefined,
              }}
            />
          )
        })}

        {routeGeometry?.length > 1 && (
          <Polyline
            positions={routeGeometry.map((p) => [p.latitude, p.longitude])}
            pathOptions={{ color: '#66f2d5', weight: 6, opacity: 0.95, lineCap: 'round', lineJoin: 'round' }}
          />
        )}

        {nodes.map((n, i) => (
          <CircleMarker
            key={n.id}
            center={[n.latitude, n.longitude]}
            radius={n.id === sourceId || n.id === destinationId ? 9 : 6}
            pathOptions={{
              color: '#07111d',
              weight: 3,
              fillColor: n.id === sourceId ? '#ffbf69' : n.id === destinationId ? '#66f2d5' : '#8fa3c2',
              fillOpacity: 1,
            }}
          >
            <Tooltip permanent direction="top" offset={[0, -8]}>
              {n.id === sourceId ? 'FROM · ' : n.id === destinationId ? 'TO · ' : `${i + 1} · `}{n.name}
            </Tooltip>
          </CircleMarker>
        ))}
      </MapContainer>

      <div className="map-overlay">
        <span className="live-dot" />
        DYNAMIC GRAPH · {nodes.length} NODES
      </div>
    </div>
  )
}
