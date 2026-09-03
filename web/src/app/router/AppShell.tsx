import { useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'

import { Button } from '../../components/ui'
import { useAuth } from '../../features/auth'
import { AddFillDialog } from '../../features/fills'
import type { AppOutletContext } from './outlet-context'

const navClassName = ({ isActive }: { isActive: boolean }) =>
  [
    'relative px-2 py-2 text-[13px] font-medium transition-colors',
    'focus-visible:rounded-control focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
    isActive
      ? 'text-accent after:absolute after:inset-x-2 after:-bottom-[13px] after:h-px after:bg-accent sm:after:-bottom-[21px]'
      : 'text-ink-3 hover:text-ink',
  ].join(' ')

export function AppShell() {
  const { email, signOut } = useAuth()
  const [fillOpen, setFillOpen] = useState(false)

  return (
    <div className="min-h-dvh bg-ground text-ink">
      <header className="border-b border-line bg-surface">
        <div className="mx-auto grid min-h-[62px] w-full max-w-screen-2xl grid-cols-2 items-center gap-x-4 px-4 py-3 sm:grid-cols-[1fr_auto_1fr] sm:px-6 sm:py-5 lg:px-8">
          <NavLink
            aria-label="Trade Ledger positions"
            className="font-wordmark w-fit text-ink"
            to="/positions"
          >
            Trade Ledger
          </NavLink>

          <nav
            aria-label="Primary navigation"
            className="col-span-2 row-start-2 mt-2 flex items-center justify-center gap-5 border-t border-line pt-2 sm:col-span-1 sm:col-start-2 sm:row-start-1 sm:mt-0 sm:border-0 sm:pt-0"
          >
            <NavLink className={navClassName} to="/positions">
              Positions
            </NavLink>
            <NavLink className={navClassName} to="/explain">
              Explain
            </NavLink>
          </nav>

          <div className="col-start-2 row-start-1 flex items-center justify-end gap-1 sm:col-start-3">
            <span className="mr-2 hidden max-w-44 truncate text-xs text-ink-3 lg:inline" title={email ?? undefined}>
              {email}
            </span>
            <Button onClick={() => setFillOpen(true)} size="sm">
              + Add fill
            </Button>
            <Button aria-label="Log out" onClick={signOut} size="sm" variant="ghost">
              Log out
            </Button>
          </div>
        </div>
      </header>

      <Outlet context={{ openAddFill: () => setFillOpen(true) } satisfies AppOutletContext} />

      <AddFillDialog onOpenChange={setFillOpen} open={fillOpen} />
    </div>
  )
}
