/**
 * Tracks open dialogs so that only the top-most one handles Escape/Tab when dialogs are stacked
 * (e.g. a confirmation on top of a form).
 */
const stack: symbol[] = []

export const overlayCount = () => stack.length

export function pushOverlay(): { isTop: () => boolean; pop: () => void } {
  const id = Symbol('overlay')
  stack.push(id)
  return {
    isTop: () => stack[stack.length - 1] === id,
    pop: () => {
      const i = stack.indexOf(id)
      if (i >= 0) stack.splice(i, 1)
    },
  }
}
