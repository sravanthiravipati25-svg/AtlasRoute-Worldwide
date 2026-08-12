# 🌍 AtlasRoute — Worldwide Graph Routing Lab

An interactive graph-routing laboratory: search **any real place on Earth**,
build a live weighted road graph between the locations you pick, and watch
Dijkstra, A*, and route comparison run over it — with adjustable priority
(Fastest / Cheapest / Shortest) and toggleable road closures.

## What's implemented, by phase

| Phase | Feature | Where |
|---|---|---|
| 1 | Dynamic worldwide location search | `GET /api/world/search` → Nominatim (OpenStreetMap) geocoding, no fixed location list |
| 2 | Dynamic graph generation | `POST /api/world/graph/build` → fetches a real OSRM route between every pair of picked locations and assembles a weighted graph on the fly |
| 3 | Distance + travel time + traffic + toll | Real distance/duration from OSRM; congestion level and toll cost simulated per edge (deterministic per node pair — see note below) |
| 4 | Fastest / Cheapest / Shortest selector | `POST /api/world/graph/route` — same Dijkstra implementation, different edge-weight function per priority |
| 5 | Dynamic road closures | Click any edge in the Graph Lab's road list to open/close it; closed edges are excluded from the graph before routing |
| 6 | Dijkstra/A* visualization | `GraphMap.jsx` — nodes, all candidate roads (colored by congestion, dashed if closed), and the computed route highlighted on a real Leaflet/OpenStreetMap map |
| 7 | Compare all three routes side-by-side | `POST /api/world/graph/compare` — runs Fastest, Cheapest, Shortest and A* together; the UI shows tabs to flip between them on the same map |
| 8 | Polished React UI | New "Graph Lab" view replaces the old fixed-demo lab; dark/mint AtlasRoute theme throughout |

## Architecture

```
backend/TrafficRouteOptimizer.API/
├── Controllers/
│   ├── WorldController.cs        search, single OSRM route, graph build/route/compare
│   ├── LocationsController.cs    \_ original static-seed CRUD API (unchanged,
│   ├── RoadSegmentsController.cs /  still usable via Swagger — not wired into the new UI)
│   └── RoutesController.cs       Dijkstra/A*/BFS over the static seeded city
├── Services/
│   ├── WorldRoutingService.cs    Nominatim geocoding + OSRM routing (single segment)
│   ├── WorldGraphService.cs      NEW — builds the dynamic multi-node graph, runs
│   │                              Dijkstra/A*, applies closures, compares priorities
│   ├── RouteService.cs / GraphService.cs   original static-graph algorithms
├── DTOs/WorldGraphDtos.cs        NEW — request/response contracts for the graph lab
└── Data/                          EF Core + SQL Server, seeded static demo city

frontend/traffic-route-ui/src/
├── components/
│   ├── GraphLab.jsx               NEW — the workspace: add locations, build graph,
│   │                                pick priority/A*/compare, toggle closures
│   ├── GraphMap.jsx                NEW — Leaflet map for the dynamic graph
│   ├── LocationSearch.jsx          worldwide search box (reused from World Live)
│   ├── WorldMap.jsx / WorldRoutePanel.jsx   original single-route "World Live" mode
│   └── RouteForm.jsx / MapView.jsx / RouteSummary.jsx   original static-lab UI
│                                      (no longer rendered by App.jsx, kept for reference)
└── App.jsx                        two modes: "World Live" (single point-to-point
                                     route) and "Graph Lab" (the new multi-node lab)
```

### How the dynamic graph is built

1. You search and add 2–8 real-world locations (any city, landmark, or address).
2. **Build Graph** calls OSRM once per unordered pair of locations (`n·(n−1)/2`
   calls) to get real driving distance and time, then assigns each edge a
   simulated congestion level (Low/Medium/High/Severe) and toll cost. These are
   simulated because free real-time traffic and toll data isn't available —
   they're deterministic per node pair (a stable hash of the two location IDs),
   so the same pair of cities always gets the same congestion/toll within a
   run, rather than being random noise on every request.
3. You pick a source, destination, and priority (or A*, or "compare all"), and
   optionally click roads in the list to mark them closed.
4. **Find Route** rebuilds the graph server-side (so closures and priority are
   always evaluated fresh) and runs Dijkstra — or A* with a haversine
   straight-line heuristic to the destination — over it, excluding closed edges.

### Endpoints added in this update

- `POST /api/world/graph/build` — `{ nodes: [{id,name,latitude,longitude}], profile }` → `{ nodes, edges, note }`
- `POST /api/world/graph/route` — `{ nodes, sourceId, destinationId, priority, useAStar, closedEdgeKeys, profile }` → path, per-segment breakdown, totals, geometry
- `POST /api/world/graph/compare` — same input minus priority → `{ fastest, cheapest, shortest, aStar }`, each a full route result

## Running it

Backend (ASP.NET Core 8 + SQL Server, per your existing setup):
```bash
cd backend/TrafficRouteOptimizer.API
dotnet restore
dotnet ef database update   # if not already applied
dotnet run
```

Frontend (React + Vite):
```bash
cd frontend/traffic-route-ui
npm install
npm run dev
```

Open the app, switch to **Graph Lab**, search and add a few places (e.g. two
or three cities), click **Build Graph**, pick a source/destination and
priority, and click **Find Route**. Toggle **Compare** to see all four
strategies at once, or click a road in the list to close it and re-route
around the closure.

## Notes & honest limitations

- OSRM's public demo server (`router.project-osrm.org`) and Nominatim's public
  API are free, rate-limited, best-effort services — fine for a lab/demo, not
  for production traffic. Swap `OpenStreetMap:OsrmUrl` / `OpenStreetMap:NominatimUrl`
  in `appsettings.json` for a self-hosted or paid provider if you need
  reliability guarantees.
- Congestion and toll are simulated, as noted above — there's no free source
  of real-time traffic or toll pricing. If you get access to one (e.g. a
  paid traffic API), swap the `SimulateTraffic` method in `WorldGraphService.cs`
  for a real call.
- Graph Lab caps at 8 locations per graph to keep the number of OSRM calls
  (`n·(n−1)/2`) reasonable against the public rate limits.


## Enhanced World Route Planner

The World Live screen now compares Drive, Cycle and Walk in one calculation and presents:
- side-by-side mode cards with ETA and distance
- mode-aware ETA fallback when the configured OSRM provider returns identical profile durations
- fastest, shortest, eco and low-cost planning preferences
- avoid-tolls and avoid-highways UI preferences for driving
- estimated driving running cost and activity calories
- clearer route status, endpoint context and provider/estimate labels
- responsive comparison cards for desktop and mobile

### Important data note
OpenStreetMap/OSRM provides the road geometry and routing response. The public OSRM demo service does not guarantee live traffic, toll pricing, fuel prices or profile-specific support. When the provider returns identical profile durations, AtlasRoute explicitly labels the mode ETA as an estimate instead of pretending the value is live traffic data.
