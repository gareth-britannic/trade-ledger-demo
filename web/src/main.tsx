import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import '@fontsource-variable/ibm-plex-sans/wght.css'
import '@fontsource/ibm-plex-mono/latin-400.css'
import '@fontsource/ibm-plex-mono/latin-500.css'
import '@fontsource/ibm-plex-mono/latin-600.css'

import { AppProviders } from './app/providers/AppProviders'
import { AppRoutes } from './app/router/AppRoutes'
import './styles/index.css'

const root = document.getElementById('root')

if (!root) throw new Error('Trade Ledger root element was not found.')

createRoot(root).render(
  <StrictMode>
    <BrowserRouter>
      <AppProviders>
        <AppRoutes />
      </AppProviders>
    </BrowserRouter>
  </StrictMode>,
)
