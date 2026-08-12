function formatDuration(minutes) {
  if (minutes < 60) return `${Math.max(1, Math.round(minutes))} min`
  const h = Math.floor(minutes / 60)
  const m = Math.round(minutes % 60)
  return `${h}h ${m}m`
}

const MODE_META = {
  driving: { label: 'Drive', icon: '🚗' },
  cycling: { label: 'Cycle', icon: '🚲' },
  walking: { label: 'Walk', icon: '🚶' },
}

function ModeCard({ item, selected, onClick }) {
  const meta = MODE_META[item.profile] || MODE_META.driving
  if (item.error || !item.route?.found) {
    return (
      <button className={`mode-card mode-unavailable ${selected ? 'mode-card-selected' : ''}`} onClick={onClick}>
        <div className="mode-card-title"><span>{meta.icon}</span><b>{meta.label}</b></div>
        <small>{item.error || 'No route found'}</small>
      </button>
    )
  }

  const route = item.route
  return (
    <button className={`mode-card ${selected ? 'mode-card-selected' : ''}`} onClick={onClick}>
      <div className="mode-card-title">
        <span>{meta.icon}</span>
        <b>{meta.label}</b>
        {selected && <em>SELECTED</em>}
      </div>
      <strong>{formatDuration(route.displayDurationMinutes ?? route.totalDurationMinutes)}</strong>
      <div className="mode-card-stats">
        <span>{route.totalDistanceKm} km</span>
        <span>{route.durationEstimated ? 'estimated ETA' : 'provider ETA'}</span>
      </div>
      <div className="mode-card-extra">
        {item.profile === 'driving' && <span>~₹{Math.round(route.estimatedFuelCost || 0)} running cost</span>}
        {item.profile === 'cycling' && <span>~{Math.round(route.calories || 0)} kcal</span>}
        {item.profile === 'walking' && <span>~{Math.round(route.calories || 0)} kcal</span>}
      </div>
    </button>
  )
}

export default function WorldRoutePanel({
  source,
  destination,
  route,
  mode,
  modeResults,
  selectedMode,
  priority,
  avoidTolls,
  avoidHighways,
  error,
  loading,
  compareLoading,
  onModeSelect,
}) {
  if (loading) {
    return (
      <aside className="insight-panel">
        <div className="panel-kicker">ROUTE ENGINE</div>
        <div className="loading-card">
          <span className="pulse-ring" />
          <div>
            <strong>Comparing Drive · Cycle · Walk</strong>
            <span>Querying the live OpenStreetMap road network for all three modes…</span>
          </div>
        </div>
      </aside>
    )
  }

  if (error) {
    return (
      <aside className="insight-panel">
        <div className="panel-kicker">ROUTE ENGINE</div>
        <div className="error-card">
          <span>!</span>
          <div>
            <strong>Route unavailable</strong>
            <p>{error}</p>
          </div>
        </div>
      </aside>
    )
  }

  if (!route) {
    return (
      <aside className="insight-panel">
        <div className="panel-kicker">ROUTE INTELLIGENCE</div>
        <h2>Ready for a smarter journey.</h2>
        <p className="muted-copy">
          Enter two places and AtlasRoute will compare the available road
          networks, then show the trade-off between time, distance and cost.
        </p>

        <div className="feature-stack">
          <div><b>01</b><span>Worldwide search</span></div>
          <div><b>02</b><span>Drive / cycle / walk comparison</span></div>
          <div><b>03</b><span>Distance + ETA + route steps</span></div>
          <div><b>04</b><span>Fastest / shortest / eco / low-cost view</span></div>
        </div>
      </aside>
    )
  }

  const effectiveMinutes = route.displayDurationMinutes ?? route.totalDurationMinutes
  const trafficLabel = mode === 'driving' ? 'Traffic-aware estimate' : 'Mode-aware estimate'

  return (
    <aside className="insight-panel">
      <div className="panel-kicker">ROUTE INTELLIGENCE</div>
      <div className="route-title-row">
        <div>
          <h2>Journey computed.</h2>
          <p>{MODE_META[mode]?.icon} {MODE_META[mode]?.label} · {priority}</p>
        </div>
        <span className="route-badge">{route.durationEstimated ? 'ESTIMATE' : 'LIVE'}</span>
      </div>

      <div className="mode-comparison">
        <div className="comparison-head">
          <span>Choose your travel mode</span>
          <small>{compareLoading ? 'updating…' : `${modeResults.filter(x => x.route?.found).length}/3 available`}</small>
        </div>
        <div className="mode-card-grid">
          {modeResults.map((item) => (
            <ModeCard
              key={item.profile}
              item={item}
              selected={item.profile === mode}
              onClick={() => onModeSelect?.(item.profile)}
            />
          ))}
        </div>
      </div>

      <div className="hero-stats">
        <div>
          <span className="big-number">{route.totalDistanceKm}</span>
          <small>KM</small>
        </div>
        <div>
          <span className="big-number">{formatDuration(effectiveMinutes)}</span>
          <small>ETA</small>
        </div>
      </div>

      <div className="metric-strip">
        <div><b>{trafficLabel}</b><span>{route.durationEstimated ? 'Provider returned identical profiles; mode speed estimate applied.' : 'Based on selected routing profile.'}</span></div>
        {mode === 'driving' && <div><b>Running cost</b><span>~₹{Math.round(route.estimatedFuelCost || 0)} · tolls depend on provider data</span></div>}
        {mode !== 'driving' && <div><b>Activity</b><span>~{Math.round(route.calories || 0)} kcal</span></div>}
      </div>

      {(avoidTolls || avoidHighways) && (
        <div className="notice-strip">
          {avoidTolls && <span>✓ Avoid tolls preference</span>}
          {avoidHighways && <span>✓ Avoid highways preference</span>}
          <small>Provider-specific avoidance is enabled in the planner UI; live OSRM public data may not expose every restriction.</small>
        </div>
      )}

      <div className="route-endpoints">
        <div>
          <i className="endpoint-dot from" />
          <span><small>FROM</small><strong>{source?.name}</strong></span>
        </div>
        <div className="endpoint-line" />
        <div>
          <i className="endpoint-dot to" />
          <span><small>TO</small><strong>{destination?.name}</strong></span>
        </div>
      </div>

      <div className="steps-head">
        <span>Route steps</span>
        <span>{route.steps?.length || 0} segments</span>
      </div>

      <div className="steps-list">
        {(route.steps || []).slice(0, 10).map((step, index) => (
          <div className="step" key={`${step.roadName}-${index}`}>
            <span className="step-index">{String(index + 1).padStart(2, '0')}</span>
            <div>
              <strong>{step.instruction}</strong>
              <small>{step.distanceKm} km · {formatDuration(step.durationMinutes)}</small>
            </div>
          </div>
        ))}
      </div>

      {route.steps?.length > 10 && (
        <p className="more-steps">+ {route.steps.length - 10} more route segments</p>
      )}

      <div className="provider-note">
        <span>◎</span>
        Live road geometry · OpenStreetMap / OSRM
      </div>
    </aside>
  )
}
