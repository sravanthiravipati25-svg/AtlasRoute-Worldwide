const PRIORITIES = [
  { value: 'Fastest', label: 'Fastest', hint: 'least travel time' },
  { value: 'Cheapest', label: 'Cheapest', hint: 'least toll cost' },
  { value: 'Shortest', label: 'Shortest', hint: 'least distance' },
]

export default function RouteForm({
  locations,
  sourceId,
  destinationId,
  priority,
  useAStar,
  loading,
  onSourceChange,
  onDestinationChange,
  onPriorityChange,
  onToggleAStar,
  onSubmit,
}) {
  return (
    <form className="panel route-form" onSubmit={onSubmit}>
      <div className="panel-eyebrow">Console</div>
      <h2 className="panel-title">Plan a route</h2>

      <label className="field">
        <span className="field-label">From</span>
        <select value={sourceId} onChange={(e) => onSourceChange(Number(e.target.value))}>
          {locations.map((l) => (
            <option key={l.id} value={l.id}>{l.name}</option>
          ))}
        </select>
      </label>

      <label className="field">
        <span className="field-label">To</span>
        <select value={destinationId} onChange={(e) => onDestinationChange(Number(e.target.value))}>
          {locations.map((l) => (
            <option key={l.id} value={l.id}>{l.name}</option>
          ))}
        </select>
      </label>

      <div className="field">
        <span className="field-label">Priority</span>
        <div className="segmented" role="radiogroup" aria-label="Route priority">
          {PRIORITIES.map((p) => (
            <button
              type="button"
              key={p.value}
              role="radio"
              aria-checked={priority === p.value}
              className={`segment ${priority === p.value ? 'segment-active' : ''}`}
              onClick={() => onPriorityChange(p.value)}
              disabled={useAStar}
            >
              <span className="segment-label">{p.label}</span>
              <span className="segment-hint">{p.hint}</span>
            </button>
          ))}
        </div>
      </div>

      <label className="field field-inline">
        <input type="checkbox" checked={useAStar} onChange={onToggleAStar} />
        <span>Use A* search instead (shortest distance, heuristic-guided)</span>
      </label>

      <button type="submit" className="btn-primary" disabled={loading || sourceId === destinationId}>
        {loading ? 'Calculating…' : 'Find route'}
      </button>

      {sourceId === destinationId && (
        <p className="field-error">Pick two different locations.</p>
      )}
    </form>
  )
}
