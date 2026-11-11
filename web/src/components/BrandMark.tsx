import { useId } from 'react'

/** LIMS logo mark: a flask on a dark-green-to-turquoise tile. */
export function BrandMark({ className = 'brand-mark' }: { className?: string }) {
  const id = useId()
  return (
    <svg className={className} viewBox="0 0 32 32" role="img" aria-label="LIMS">
      <defs>
        <linearGradient id={id} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#075247" />
          <stop offset="1" stopColor="#14A99A" />
        </linearGradient>
      </defs>
      <rect width="32" height="32" rx="8" fill={`url(#${id})`} />
      <path
        d="M13 7h6M14 7v6.2l-5 8.6A2.1 2.1 0 0 0 10.8 25h10.4a2.1 2.1 0 0 0 1.8-3.2l-5-8.6V7"
        fill="none"
        stroke="#fff"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path d="M11.6 19.5h8.8" stroke="#fff" strokeWidth="2" strokeLinecap="round" />
    </svg>
  )
}
