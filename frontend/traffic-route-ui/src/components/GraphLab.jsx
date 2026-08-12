import { useState } from 'react'
import { api } from '../services/api'
import LocationSearch from './LocationSearch'
import GraphMap from './GraphMap'

const PRIORITIES = [
  { value: 'Fastest', label: 'Fastest', hint: 'least travel time' },
  { value: 'Cheapest', label: 'Cheapest', hint: 'least toll cost' },
  { value: 'Shortest', label: 'Shortest', hint: 'least distance' },
]

function formatDuration(minutes) {
  if (minutes < 60) return `${Math.round(minutes)} min`
  const h = Math.floor(minutes / 60)
  const m = Math.round(minutes % 60)
  return `${h}h ${m}m`
}

function edgeKey(e) { return `${e.fromId}-${e.toId}` }

export default function GraphLab() {
  const [nodes, setNodes] = useState([])
  const [edges, setEdges] = useState([])
  const [note, setNote] = useState('')
  const [buildingGraph, setBuildingGraph] = useState(false)
  const [buildError, setBuildError] = useState('')

  const [sourceId, setSourceId] = useState('')
  const [destinationId, setDestinationId] = useState('')
  const [priority, setPriority] = useState('Fastest')
  const [useAStar, setUseAStar] = useState(false)
  const [compareMode, setCompareMode] = useState(false)
  const [closedKeys, setClosedKeys] = useState(new Set())

  const [singleResult, setSingleResult] = useState(null)
  const [compareResult, setCompareResult] = useState(null)
  const [activeCompareKey, setActiveCompareKey] = useState('fastest')
  const [routing, setRouting] = useState(false)
  const [routeError, setRouteError] = useState('')

  function addNode(place) {
    if (!place || nodes.some((n) => n.id === place.id)) return
    if (nodes.length >= 8) return
    const next = [...nodes, { id: place.id, name: place.name || place.displayName.split(',')[0], latitude: place.latitude, longitude: place.longitude }]
    setNodes(next)
    setEdges([])
    setSingleResult(null)
    setCompareResult(null)
  }

  function removeNode(id) {
    setNodes(nodes.filter((n) => n.id !== id))
    setEdges([])
    setSingleResult(null)
    setCompareResult(null)
    if (sourceId === id) setSourceId('')
    if (destinationId === id) setDestinationId('')
  }

  async function buildGraph() {
    setBuildingGraph(true)
    setBuildError('')
    try {
      const data = await api.buildWorldGraph(nodes)
      setEdges(data.edges)
      setNote(data.note)
      if (!sourceId && data.edges.length) setSourceId(nodes[0].id)
      if (!destinationId && nodes.length > 1) setDestinationId(nodes[1].id)
    } catch (err) {
      setBuildError(err.message)
    } finally {
      setBuildingGraph(false)
    }
  }

  function toggleClosed(key) {
    const next = new Set(closedKeys)
    if (next.has(key)) next.delete(key)
    else next.add(key)
    setClosedKeys(next)
  }

  async function findRoute() {
    if (!sourceId || !destinationId || sourceId === destinationId) return
    setRouting(true)
    setRouteError('')
    setSingleResult(null)
    setCompareResult(null)

    try {
      if (compareMode) {
        const data = await api.compareWorldGraph(nodes, sourceId, destinationId, [...closedKeys])
        setCompareResult(data)
        setActiveCompareKey('fastest')
      } else {
        const data = await api.routeOnWorldGraph(nodes, sourceId, destinationId, priority, useAStar, [...closedKeys])
        setSingleResult(data)
      }
    } catch (err) {
      setRouteError(err.message)
    } finally {
      setRouting(false)
    }
  }

  const displayedEdges = edges.map((e) => ({ ...e, isClosed: closedKeys.has(edgeKey(e)) || closedKeys.has(`${e.toId}-${e.fromId}`) }))

  const activeResult = compareMode
    ? compareResult?.[{ fastest: 'fastest', cheapest: 'cheapest', shortest: 'shortest', astar: 'aStar' }[activeCompareKey]]
    : singleResult

  return (
    <main className="graph-lab-layout">
      <section className="control-panel graph-panel">
        <div className="panel-kicker">GRAPH LAB</div>
        <h1>Build a graph.<br /><em>Anywhere on Earth.</em></h1>
        <p className="intro">
          Search and add up to 8 real-world locations. AtlasRoute fetches live
          road distance and time between every pair, layers on simulated
          congestion and toll, then runs Dijkstra or A* over the graph you built.
        </p>

        <LocationSearch key={nodes.length} label="Add a location" icon="from" value={null} onSelect={addNode} />

        {nodes.length > 0 && (
          <div className="node-chip-list">
            {nodes.map((n, i) => (
              <span className="node-chip" key={n.id}>
                <b>{i + 1}</b> {n.name}
                <button type="button" onClick={() => removeNode(n.id)} aria-label={`Remove ${n.name}`}>×</button>
              </span>
            ))}
          </div>
        )}

        <button className="route-btn" type="button" disabled={nodes.length < 2 || buildingGraph} onClick={buildGraph}>
          {buildingGraph ? 'BUILDING GRAPH…' : `BUILD GRAPH (${nodes.length} nodes) →`}
        </button>
        {buildError && <p className="summary-error">{buildError}</p>}

        {edges.length > 0 && (
          <>
            <div className="mode-row">
              <span>From</span>
              <select value={sourceId} onChange={(e) => setSourceId(e.target.value)}>
                <option value="">Select…</option>
                {nodes.map((n) => <option key={n.id} value={n.id}>{n.name}</option>)}
              </select>
            </div>
            <div className="mode-row">
              <span>To</span>
              <select value={destinationId} onChange={(e) => setDestinationId(e.target.value)}>
                <option value="">Select…</option>
                {nodes.map((n) => <option key={n.id} value={n.id}>{n.name}</option>)}
              </select>
            </div>

            <div className="segmented">
              {PRIORITIES.map((p) => (
                <button
                  type="button"
                  key={p.value}
                  className={`segment ${priority === p.value && !useAStar && !compareMode ? 'segment-active' : ''}`}
                  disabled={useAStar || compareMode}
                  onClick={() => setPriority(p.value)}
                >
                  <span className="segment-label">{p.label}</span>
                  <span className="segment-hint">{p.hint}</span>
                </button>
              ))}
            </div>

            <label className="field-inline">
              <input type="checkbox" checked={useAStar} disabled={compareMode} onChange={() => setUseAStar((v) => !v)} />
              <span>Use A* search (shortest distance, heuristic-guided)</span>
            </label>
            <label className="field-inline">
              <input type="checkbox" checked={compareMode} onChange={() => setCompareMode((v) => !v)} />
              <span>Compare Fastest / Cheapest / Shortest / A* side-by-side</span>
            </label>

            <button className="route-btn" type="button" disabled={!sourceId || !destinationId || sourceId === destinationId || routing} onClick={findRoute}>
              {routing ? 'ROUTING…' : 'FIND ROUTE →'}
            </button>
            {routeError && <p className="summary-error">{routeError}</p>}

            <div className="edge-table-wrap">
              <div className="edge-table-head">
                <span>Roads in this graph</span>
                <span>click to open/close</span>
              </div>
              <div className="edge-table">
                {displayedEdges.filter((e, i, arr) => arr.findIndex((x) => edgeKey(x) === edgeKey(e) || edgeKey(x) === `${e.toId}-${e.fromId}`) === i).map((e) => {
                  const from = nodes.find((n) => n.id === e.fromId)
                  const to = nodes.find((n) => n.id === e.toId)
                  const key = edgeKey(e)
                  return (
                    <button type="button" key={key} className={`edge-row ${e.isClosed ? 'edge-row-closed' : ''}`} onClick={() => toggleClosed(key)}>
                      <span>{from?.name} ↔ {to?.name}</span>
                      <span className={`tag tag-${e.congestion.toLowerCase()}`}>{e.congestion}</span>
                      <span>{e.distanceKm} km</span>
                      <span>{e.tollCost ? `₹${e.tollCost}` : '—'}</span>
                      <span>{e.isClosed ? 'CLOSED' : 'open'}</span>
                    </button>
                  )
                })}
              </div>
            </div>
            {note && <p className="muted-copy small-note">{note}</p>}
          </>
        )}
      </section>

      <section className="map-panel-world">
        <GraphMap
          nodes={nodes}
          edges={displayedEdges}
          sourceId={sourceId}
          destinationId={destinationId}
          routeGeometry={activeResult?.geometry}
        />
      </section>

      <aside className="insight-panel">
        <div className="panel-kicker">ROUTE INTELLIGENCE</div>

        {!singleResult && !compareResult && (
          <>
            <h2>Ready to route.</h2>
            <p className="muted-copy">Add locations, build the graph, then find a route.</p>
          </>
        )}

        {compareResult && (
          <>
            <h2>Four ways there.</h2>
            <div className="compare-tabs">
              {[
                ['fastest', 'Fastest'],
                ['cheapest', 'Cheapest'],
                ['shortest', 'Shortest'],
                ['astar', 'A*'],
              ].map(([key, label]) => (
                <button
                  key={key}
                  type="button"
                  className={activeCompareKey === key ? 'active' : ''}
                  onClick={() => setActiveCompareKey(key)}
                >
                  {label}
                </button>
              ))}
            </div>
          </>
        )}

        {activeResult?.found && (
          <>
            <div className="hero-stats">
              <div><span className="big-number">{activeResult.totalDistanceKm}</span><small>KM</small></div>
              <div><span className="big-number">{formatDuration(activeResult.totalDurationMinutes)}</span><small>TIME</small></div>
              <div><span className="big-number">₹{activeResult.totalTollCost}</span><small>TOLL</small></div>
            </div>

            <div className="route-path">
              {activeResult.pathNodeNames.map((name, i) => (
                <span key={i} className="path-chip">
                  {name}{i < activeResult.pathNodeNames.length - 1 && <span className="path-arrow">→</span>}
                </span>
              ))}
            </div>

            <div className="steps-head">
              <span>Segments</span>
              <span>{activeResult.steps.length}</span>
            </div>
            <div className="steps-list">
              {activeResult.steps.map((s, i) => (
                <div className="step" key={i}>
                  <span className="step-index">{String(i + 1).padStart(2, '0')}</span>
                  <div>
                    <strong>{s.fromName} → {s.toName}</strong>
                    <small>{s.distanceKm} km · {formatDuration(s.durationMinutes)} · {s.congestion} traffic{s.tollCost ? ` · ₹${s.tollCost} toll` : ''}</small>
                  </div>
                </div>
              ))}
            </div>
          </>
        )}

        {activeResult && !activeResult.found && (
          <div className="error-card">
            <span>!</span>
            <div><strong>No route</strong><p>{activeResult.message}</p></div>
          </div>
        )}
      </aside>
    </main>
  )
}
