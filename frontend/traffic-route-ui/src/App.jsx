import { useMemo, useState } from 'react'
import { api } from './services/api'
import LocationSearch from './components/LocationSearch'
import WorldMap from './components/WorldMap'
import WorldRoutePanel from './components/WorldRoutePanel'
import GraphLab from './components/GraphLab'

const MODES = [
  { value: 'driving', label: 'Drive', icon: '🚗', speedKph: 45, costPerKm: 0.14 },
  { value: 'cycling', label: 'Cycle', icon: '🚲', speedKph: 18, costPerKm: 0.02 },
  { value: 'walking', label: 'Walk', icon: '🚶', speedKph: 5, costPerKm: 0 },
]

function modeInfo(profile) {
  return MODES.find((m) => m.value === profile) || MODES[0]
}

function enrichModeResult(item) {
  if (!item.route?.found) return item

  const meta = modeInfo(item.profile)
  const distance = Number(item.route.totalDistanceKm || 0)
  const providerMinutes = Number(item.route.totalDurationMinutes || 0)
  const estimates = MODES.filter(Boolean)
  const modeTimes = {
    driving: Math.max(1, distance / 45 * 60),
    cycling: Math.max(1, distance / 18 * 60),
    walking: Math.max(1, distance / 5 * 60),
  }
  const allProviderTimes = estimates.map((m) => Number(item.providerTimes?.[m.value] || 0)).filter(Boolean)
  const providerLooksDuplicated = allProviderTimes.length >= 2 && Math.max(...allProviderTimes) - Math.min(...allProviderTimes) < 0.5

  return {
    ...item,
    route: {
      ...item.route,
      displayDurationMinutes: providerLooksDuplicated ? modeTimes[item.profile] : providerMinutes,
      durationEstimated: providerLooksDuplicated,
      estimatedFuelCost: item.profile === 'driving' ? distance * meta.costPerKm : distance * meta.costPerKm,
      estimatedCo2Grams: item.profile === 'driving' ? distance * 192 : item.profile === 'cycling' ? distance * 8 : 0,
      calories: item.profile === 'walking' ? distance * 52 : item.profile === 'cycling' ? distance * 30 : 0,
    },
  }
}

export default function App() {
  const [mode, setMode] = useState('world')
  const [source, setSource] = useState(null)
  const [destination, setDestination] = useState(null)
  const [profile, setProfile] = useState('driving')
  const [priority, setPriority] = useState('fastest')
  const [avoidTolls, setAvoidTolls] = useState(false)
  const [avoidHighways, setAvoidHighways] = useState(false)
  const [worldRoute, setWorldRoute] = useState(null)
  const [modeResults, setModeResults] = useState([])
  const [worldError, setWorldError] = useState('')
  const [worldLoading, setWorldLoading] = useState(false)
  const [compareLoading, setCompareLoading] = useState(false)

  const selectedMode = useMemo(() => modeInfo(profile), [profile])

  async function calculateWorldRoute(e) {
    e.preventDefault()
    if (!source || !destination) return

    setWorldLoading(true)
    setWorldError('')

    try {
      const data = await api.worldRoute(source, destination, profile)
      const enriched = enrichModeResult({ profile, route: data })
      setWorldRoute(enriched.route)

      // Compare all three modes so users can see that the mode choice matters.
      setCompareLoading(true)
      const comparison = await api.compareWorldModes(source, destination)
      const rawTimes = comparison.map((x) => Number(x.route?.totalDurationMinutes || 0))
      const providerTimes = {
        driving: rawTimes[0],
        cycling: rawTimes[1],
        walking: rawTimes[2],
      }
      setModeResults(comparison.map((x) => enrichModeResult({ ...x, providerTimes })))
    } catch (error) {
      setWorldError(error.message)
      setWorldRoute(null)
      setModeResults([])
    } finally {
      setWorldLoading(false)
      setCompareLoading(false)
    }
  }

  function swapPlaces() {
    setSource(destination)
    setDestination(source)
    setWorldRoute(null)
    setModeResults([])
    setWorldError('')
  }

  function selectMode(next) {
    setProfile(next)
    const match = modeResults.find((x) => x.profile === next)
    if (match?.route?.found) setWorldRoute(match.route)
    else setWorldRoute(null)
    setWorldError('')
  }

  function selectPriority(next) {
    setPriority(next)
    // Priority changes are intentionally visible in the UI. The live OSRM route
    // remains the provider route; cost/eco labels are estimates until a traffic
    // and toll provider is configured.
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">
          <div className="brand-symbol">⌁</div>
          <div>
            <div className="brand-name">ATLAS<span>ROUTE</span></div>
            <div className="brand-tagline">Global routing · graph intelligence</div>
          </div>
        </div>

        <div className="mode-switch">
          <button className={mode === 'world' ? 'active' : ''} onClick={() => setMode('world')}>
            <span>◉</span> World Live
          </button>
          <button className={mode === 'graph' ? 'active' : ''} onClick={() => setMode('graph')}>
            <span>◇</span> Graph Lab
          </button>
        </div>

        <div className="status-chip">
          <span className="live-dot" />
          OPENSTREETMAP · OSRM
        </div>
      </header>

      {mode === 'world' ? (
        <main className="world-layout">
          <section className="control-panel">
            <div className="panel-kicker">WORLD ROUTE PLANNER</div>
            <h1>Go anywhere.<br /><em>Compare before you go.</em></h1>
            <p className="intro">
              Search any address, landmark, airport or city. Compare driving,
              cycling and walking before choosing your route.
            </p>

            <form onSubmit={calculateWorldRoute}>
              <LocationSearch
                label="From"
                icon="from"
                value={source}
                onSelect={(place) => {
                  setSource(place)
                  setWorldRoute(null)
                  setModeResults([])
                  setWorldError('')
                }}
              />

              <button className="swap-btn" type="button" onClick={swapPlaces} aria-label="Swap locations">
                ⇅
              </button>

              <LocationSearch
                label="To"
                icon="to"
                value={destination}
                onSelect={(place) => {
                  setDestination(place)
                  setWorldRoute(null)
                  setModeResults([])
                  setWorldError('')
                }}
              />

              <div className="mode-row">
                <span>Travel mode</span>
                <div>
                  {MODES.map((m) => (
                    <button
                      key={m.value}
                      type="button"
                      className={profile === m.value ? 'selected' : ''}
                      onClick={() => selectMode(m.value)}
                    >
                      {m.icon} {m.label}
                    </button>
                  ))}
                </div>
              </div>

              <div className="planner-options">
                <div className="option-title">Route preference</div>
                <div className="priority-grid">
                  {[
                    ['fastest', '⚡ Fastest'],
                    ['shortest', '↘ Shortest'],
                    ['eco', '🌿 Eco'],
                    ['lowcost', '₹ Low cost'],
                  ].map(([value, label]) => (
                    <button
                      key={value}
                      type="button"
                      className={priority === value ? 'option-selected' : ''}
                      onClick={() => selectPriority(value)}
                    >
                      {label}
                    </button>
                  ))}
                </div>

                {profile === 'driving' && (
                  <div className="avoid-options">
                    <label>
                      <input type="checkbox" checked={avoidTolls} onChange={(e) => setAvoidTolls(e.target.checked)} />
                      Avoid tolls
                    </label>
                    <label>
                      <input type="checkbox" checked={avoidHighways} onChange={(e) => setAvoidHighways(e.target.checked)} />
                      Avoid highways
                    </label>
                  </div>
                )}
              </div>

              <button
                className="route-btn"
                disabled={!source || !destination || worldLoading}
                type="submit"
              >
                {worldLoading ? 'CALCULATING 3 MODES…' : 'COMPARE & CALCULATE  →'}
              </button>
            </form>

            <div className="planner-footer">
              <span>◈ Worldwide geocoding</span>
              <span>◎ OpenStreetMap road network</span>
              <span>↗ Mode comparison + route intelligence</span>
            </div>
          </section>

          <section className="map-panel-world">
            <WorldMap source={source} destination={destination} route={worldRoute} />
          </section>

          <WorldRoutePanel
            source={source}
            destination={destination}
            route={worldRoute}
            mode={profile}
            modeResults={modeResults}
            selectedMode={selectedMode}
            priority={priority}
            avoidTolls={avoidTolls}
            avoidHighways={avoidHighways}
            error={worldError}
            loading={worldLoading}
            compareLoading={compareLoading}
            onModeSelect={selectMode}
          />
        </main>
      ) : (
        <GraphLab />
      )}
    </div>
  )
}
