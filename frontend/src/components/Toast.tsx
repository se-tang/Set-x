import { createContext, useCallback, useContext, useRef, useState } from 'react'

type ToastType = 'success' | 'error' | 'info' | 'loading'
interface Toast { id: number; type: ToastType; message: string; timeout?: number }
interface ToastCtx { toast: (type: ToastType, message: string, timeout?: number) => void }

const Ctx = createContext<ToastCtx>({ toast: () => {} })
export const useToast = () => useContext(Ctx)

let seq = 0

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const timers = useRef<Record<number, ReturnType<typeof setTimeout>>>({})

  const remove = useCallback((id: number) => {
    setToasts(t => t.filter(x => x.id !== id))
    clearTimeout(timers.current[id])
    delete timers.current[id]
  }, [])

  const toast = useCallback((type: ToastType, message: string, timeout = 3000) => {
    const id = ++seq
    setToasts(t => [...t, { id, type, message }])
    if (type !== 'loading') {
      timers.current[id] = setTimeout(() => remove(id), timeout)
    }
  }, [remove])

  const icons: Record<ToastType, string> = {
    success: '✅', error: '❌', info: 'ℹ️', loading: '⏳'
  }

  return (
    <Ctx.Provider value={{ toast }}>
      {children}
      <div className="toast-container">
        {toasts.map(t => (
          <div key={t.id} className={`toast toast-${t.type}`} onClick={() => remove(t.id)}>
            <span className="toast-icon">{icons[t.type]}</span>
            <span className="toast-msg">{t.message}</span>
            {t.type === 'loading' && <span className="toast-spinner" />}
          </div>
        ))}
      </div>
    </Ctx.Provider>
  )
}
