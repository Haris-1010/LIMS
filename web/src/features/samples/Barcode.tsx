/**
 * Draws a Code 128 barcode. The server encodes the value into a bit-string of modules ("1" = bar, "0" = space);
 * this component only paints it, so no barcode library is needed and nothing leaves the local network.
 * A 10-module quiet zone is added on both sides, as the standard requires for reliable scanning.
 */
export function Barcode({ modules, label, height = 40 }: { modules: string; label: string; height?: number }) {
  if (!modules) return null
  const quiet = 10
  const runs: { x: number; w: number }[] = []
  let start = -1
  for (let i = 0; i <= modules.length; i++) {
    const bar = modules[i] === '1'
    if (bar && start < 0) start = i
    if (!bar && start >= 0) {
      runs.push({ x: start + quiet, w: i - start })
      start = -1
    }
  }
  const width = modules.length + quiet * 2
  return (
    <svg className="barcode" viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none" role="img" aria-label={label} style={{ width: '100%', height }}>
      <rect width={width} height={height} fill="#fff" />
      {runs.map((r) => <rect key={r.x} x={r.x} y={0} width={r.w} height={height} fill="#000" shapeRendering="crispEdges" />)}
    </svg>
  )
}
