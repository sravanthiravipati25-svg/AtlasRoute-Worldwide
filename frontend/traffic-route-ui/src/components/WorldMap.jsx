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

function FitRoute({ source, destination, geometry }) {
  const map = useMap()

  useEffect(() => {
    const points = []

    if (source) points.push([source.latitude, source.longitude])
    if (destination) points.push([destination.latitude, destination.longitude])

    if (geometry?.length) {
      geometry.forEach((p) => points.push([p.latitude, p.longitude]))
    }

    if (points.length >= 2) {
      map.fitBounds(points, { padding: [70, 70], maxZoom: 14 })
    } else if (points.length === 1) {
      map.flyTo(points[0], 11, { duration: 0.8 })
    }
  }, [map, source, destination, geometry])

  return null
}

export default function WorldMap({ source, destination, route }) {
  const geometry = route?.geometry || []

  return (
    <div className="world-map-shell">
      <MapContainer
        center={[20, 0]}
        zoom={2.4}
        minZoom={2}
        maxZoom={18}
        worldCopyJump
        scrollWheelZoom
        className="world-map"
      >
        <TileLayer
          attribution='&copy; OpenStreetMap contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />

        <FitRoute
          source={source}
          destination={destination}
          geometry={geometry}
        />

        {geometry.length > 1 && (
          <Polyline
            positions={geometry.map((p) => [p.latitude, p.longitude])}
            pathOptions={{
              color: '#66f2d5',
              weight: 6,
              opacity: 0.92,
              lineCap: 'round',
              lineJoin: 'round',
            }}
          />
        )}

        {source && (
          <CircleMarker
            center={[source.latitude, source.longitude]}
            radius={9}
            pathOptions={{
              color: '#07111d',
              weight: 3,
              fillColor: '#ffbf69',
              fillOpacity: 1,
            }}
          >
            <Tooltip permanent direction="top" offset={[0, -8]}>
              FROM · {source.name}
            </Tooltip>
          </CircleMarker>
        )}

        {destination && (
          <CircleMarker
            center={[destination.latitude, destination.longitude]}
            radius={9}
            pathOptions={{
              color: '#07111d',
              weight: 3,
              fillColor: '#66f2d5',
              fillOpacity: 1,
            }}
          >
            <Tooltip permanent direction="top" offset={[0, -8]}>
              TO · {destination.name}
            </Tooltip>
          </CircleMarker>
        )}
      </MapContainer>

      <div className="map-overlay">
        <span className="live-dot" />
        LIVE WORLD MAP
      </div>
    </div>
  )
}
