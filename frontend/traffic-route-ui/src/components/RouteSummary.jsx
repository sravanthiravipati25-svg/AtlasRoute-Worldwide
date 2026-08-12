export default function RouteSummary({ result, error, loading }) {
  if (loading) {
    return (
      <div className="panel summary-panel">
        <div className="panel-eyebrow">Result</div>
        <p className="summary-empty">Computing optimal path…</p>
      </div>
    )
  }

  if (error) {
    return (
      <div className="panel summary-panel">
        <div className="panel-eyebrow">Result</div>
        <p className="summary-error">{error}</p>
      </div>
    )
  }

  if (!result) {
    return (
      <div className="panel summary-panel">
        <div className="panel-eyebrow">Result</div>
        <p className="summary-empty">Choose a source, destination, and priority, then find a route.</p>
      </div>
    )
  }

  return (
    <div className="panel summary-panel">
      <div className="panel-eyebrow">{result.algorithm} · {result.priority}</div>
      <h2 className="panel-title">Route summary</h2>

      <div className="stat-row">
        <div className="stat">
          <span className="stat-value">{result.totalDistanceKm}</span>
          <span className="stat-label">km</span>
        </div>
        <div className="stat">
          <span className="stat-value">{result.totalTravelTimeMinutes}</span>
          <span className="stat-label">minutes</span>
        </div>
        <div className="stat">
          <span className="stat-value">₹{result.totalTollCost}</span>
          <span className="stat-label">toll cost</span>
        </div>
        <div className="stat">
          <span className="stat-value">{result.hopCount}</span>
          <span className="stat-label">hops</span>
        </div>
      </div>

      <div className="route-path">
        {result.pathLocationNames.map((name, i) => (
          <span key={i} className="path-chip">
            {name}
            {i < result.pathLocationNames.length - 1 && <span className="path-arrow">→</span>}
          </span>
        ))}
      </div>

      <table className="segment-table">
        <thead>
          <tr>
            <th>Road</th>
            <th>Segment</th>
            <th>km</th>
            <th>min</th>
            <th>toll</th>
            <th>traffic</th>
          </tr>
        </thead>
        <tbody>
          {result.segments.map((s, i) => (
            <tr key={i}>
              <td>{s.roadName}</td>
              <td>{s.fromLocationName} → {s.toLocationName}</td>
              <td className="num">{s.distanceKm}</td>
              <td className="num">{Math.round(s.travelTimeMinutes)}</td>
              <td className="num">{s.tollCost ? `₹${s.tollCost}` : '—'}</td>
              <td><span className={`tag tag-${s.congestion.toLowerCase()}`}>{s.congestion}</span></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
