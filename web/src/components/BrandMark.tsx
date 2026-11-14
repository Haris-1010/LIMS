/**
 * HART LIMS logo mark: the flask emblem cropped from the supplied logo
 * (`web/HART LIMS Laboratory Software Logo.png` -> `public/brand-mark.png`).
 * Shown on the sidebar, the sign-in panel and the splash screen.
 */
export function BrandMark({ className = 'brand-mark' }: { className?: string }) {
  return <img className={className} src="/brand-mark.png" alt="HART LIMS" />
}
