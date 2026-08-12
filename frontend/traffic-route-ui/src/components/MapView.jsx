const WIDTH = 860
const HEIGHT = 560
const PADDING = 60

const CONGESTION_COLOR = {
  Low: '#3A4A63',
  Medium: '#8492A6',
  High: '#D98A3D',
  Severe: '#E5484D',
}

function project(locations) {
  const lats = locations.map((l) => l.latitude)
  const lngs = locations.map((l) => l.longitude)
  const minLat = Math.min(...lats), maxLat = Math.max(...lats)
  const minLng = Math.min(...lngs), maxLng = Math.max(...lngs)

  const spanLat = maxLat - minLat || 1
  const spanLng = maxLng - minLng || 1

  const points = {}
  locations.forEach((l) => {
    const x = PADDING + ((l.longitude - minLng) / spanLng) * (WIDTH - PADDING * 2)
    // invert latitude so north is up
    const y = HEIGHT - PADDING - ((l.latitude - minLat) / spanLat) * (HEIGHT - PADDING * 2)
    points[l.id] = { x, y }
  })
  return points
}

function dedupeEdges(roads) {
  const seen = new Set()
  const result = []
  for (const r of roads) {
    const key = [r.fromLocationId, r.toLocationId].sort((a, b) => a - b).join('-')
    if (seen.has(key)) continue
    seen.add(key)
    result.push(r)
  }
  return result
}

export default function MapView({ locations, roads, sourceId, destinationId, pathEdgeKeys }) {
  if (!locations.length) return null

  const points = project(locations)
  const uniqueRoads = dedupeEdges(roads)
  const pathSet = new Set(pathEdgeKeys || [])

  return (
    <div className="panel map-panel">
      <div className="panel-eyebrow">Network</div>
      <h2 className="panel-title">City road graph</h2>
      <svg viewBox={`0 0 ${WIDTH} ${HEIGHT}`} className="map-svg" role="img" aria-label="Road network map">
        {uniqueRoads.map((r) => {
          const a = points[r.fromLocationId]
          const b = points[r.toLocationId]
          if (!a || !b) return null
          const onPath = pathSet.has(`${r.fromLocationId}-${r.toLocationId}`) || pathSet.has(`${r.toLocationId}-${r.fromLocationId}`)
          return (
            <line
              key={r.id}
              x1={a.x} y1={a.y} x2={b.x} y2={b.y}
              className={`edge ${r.isClosed ? 'edge-closed' : ''} ${onPath ? 'edge-active' : ''}`}
              stroke={onPath ? undefined : CONGESTION_COLOR[r.congestion] || '#3A4A63'}
              strokeDasharray={r.isClosed ? '4 4' : undefined}
            />
          )
        })}

        {locations.map((l) => {
          const p = points[l.id]
          const isSource = l.id === sourceId
          const isDest = l.id === destinationId
          return (
            <g key={l.id} transform={`translate(${p.x}, ${p.y})`}>
              <circle
                r={isSource || isDest ? 9 : 5.5}
                className={`node ${isSource ? 'node-source' : ''} ${isDest ? 'node-dest' : ''}`}
              />
              <text x={0} y={-14} textAnchor="middle" className="node-label">{l.name}</text>
            </g>
          )
        })}
      </svg>

      <div className="map-legend">
        <span><i className="dot dot-source" /> Source</span>
        <span><i className="dot dot-dest" /> Destination</span>
        <span><i className="line line-active" /> Computed route</span>
        <span><i className="line line-closed" /> Road closed</span>
      </div>
    </div>
  )
}
