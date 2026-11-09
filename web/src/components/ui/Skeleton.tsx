export function Skeleton({
  width = '100%',
  height = 14,
  radius,
}: {
  width?: number | string
  height?: number | string
  radius?: number
}) {
  return <span className="skeleton" aria-hidden style={{ width, height, borderRadius: radius }} />
}
