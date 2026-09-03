import { Navigate, Route, Routes } from 'react-router-dom'

import { PublicOnlyRoute, RequireAuth, SignInPage } from '../../features/auth'
import { ExplainPage } from '../../features/explain'
import { PositionsPage } from '../../features/positions'
import { AppShell } from './AppShell'
import { useAppOutlet } from './outlet-context'

function PositionsRoute() {
  const { openAddFill } = useAppOutlet()
  return <PositionsPage onAddFill={openAddFill} />
}

export function AppRoutes() {
  return (
    <Routes>
      <Route
        path="/sign-in"
        element={
          <PublicOnlyRoute>
            <SignInPage />
          </PublicOnlyRoute>
        }
      />
      <Route
        element={
          <RequireAuth>
            <AppShell />
          </RequireAuth>
        }
      >
        <Route index element={<Navigate replace to="/positions" />} />
        <Route path="/positions" element={<PositionsRoute />} />
        <Route path="/explain" element={<ExplainPage />} />
      </Route>
      <Route path="*" element={<Navigate replace to="/positions" />} />
    </Routes>
  )
}
