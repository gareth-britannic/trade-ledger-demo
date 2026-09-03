import { useOutletContext } from 'react-router-dom'

export interface AppOutletContext {
  openAddFill: () => void
}

export function useAppOutlet(): AppOutletContext {
  return useOutletContext<AppOutletContext>()
}
