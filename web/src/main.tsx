import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'

// Fonts are bundled from npm packages, no CDN, works fully offline.
import '@fontsource-variable/inter/index.css'
import '@fontsource/noto-nastaliq-urdu/400.css'
import '@fontsource/noto-nastaliq-urdu/600.css'

import './styles/tokens.css'
import './styles/base.css'
import './styles/components.css'
import './styles/layout.css'
import './i18n'
import App from './App'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
