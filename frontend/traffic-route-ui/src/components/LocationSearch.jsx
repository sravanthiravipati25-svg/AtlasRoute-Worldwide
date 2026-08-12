import { useEffect, useRef, useState } from 'react'
import { api } from '../services/api'

export default function LocationSearch({ label, value, onSelect, icon }) {
  const [query, setQuery] = useState(value?.displayName || '')
  const [results, setResults] = useState([])
  const [loading, setLoading] = useState(false)
  const [open, setOpen] = useState(false)
  const controllerRef = useRef(null)

  useEffect(() => {
    if (value?.displayName) setQuery(value.displayName)
  }, [value?.displayName])

  useEffect(() => {
    const trimmed = query.trim()
    if (trimmed.length < 3 || trimmed === value?.displayName) {
      setResults([])
      setLoading(false)
      return
    }

    const timer = setTimeout(async () => {
      controllerRef.current?.abort()
      const controller = new AbortController()
      controllerRef.current = controller
      setLoading(true)

      try {
        const data = await api.worldSearch(trimmed, controller.signal)
        setResults(data)
        setOpen(true)
      } catch (error) {
        if (error.name !== 'AbortError') setResults([])
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }, 450)

    return () => clearTimeout(timer)
  }, [query, value?.displayName])

  function choose(place) {
    setQuery(place.displayName)
    setResults([])
    setOpen(false)
    onSelect(place)
  }

  return (
    <div className="search-wrap">
      <div className="search-label">
        <span className={`search-icon ${icon === 'to' ? 'to-icon' : 'from-icon'}`}>{icon === 'to' ? '●' : '◉'}</span>
        <span>{label}</span>
      </div>

      <div className="search-input-wrap">
        <input
          value={query}
          onChange={(e) => {
            setQuery(e.target.value)
            setOpen(true)
            if (value) onSelect(null)
          }}
          onFocus={() => results.length && setOpen(true)}
          placeholder={label === 'From' ? 'Search any city, airport, address…' : 'Search destination…'}
          aria-label={label}
        />
        {loading && <span className="search-spinner" />}
        {query && !loading && (
          <button className="clear-search" type="button" onClick={() => {
            setQuery('')
            setResults([])
            setOpen(false)
            onSelect(null)
          }}>×</button>
        )}
      </div>

      {open && results.length > 0 && (
        <div className="search-results">
          {results.map((place) => (
            <button
              className="search-result"
              type="button"
              key={place.id}
              onClick={() => choose(place)}
            >
              <span className="result-pin">⌖</span>
              <span>
                <strong>{place.name || place.displayName.split(',')[0]}</strong>
                <small>{place.displayName}</small>
              </span>
            </button>
          ))}
        </div>
      )}

      {open && !loading && query.trim().length >= 3 && results.length === 0 && (
        <div className="search-empty">No matching places found.</div>
      )}
    </div>
  )
}
