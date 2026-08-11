import { Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { useEffect, useState } from 'react'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import Servers from './pages/Servers'
import ServerDetail from './pages/ServerDetail'
import Users from './pages/Users'
import Plans from './pages/Plans'
import Nodes from './pages/Nodes'
import Subscriptions from './pages/Subscriptions'
import { ToastProvider } from './components/Toast'

export function getToken(): string | null {
  return localStorage.getItem('setx_token')
}

export function api(path: string, options: RequestInit = {}): Promise<any> {
  const token = getToken()
  return fetch(`/api${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(options.headers || {})
    }
  }).then(async (res) => {
    if (res.status === 401) {
      localStorage.removeItem('setx_token')
      window.location.href = '/login'
      throw new Error('未授权')
    }
    const text = await res.text()
    try { return JSON.parse(text) } catch { return text }
  })
}

function Layout({ children }: { children: React.ReactNode }) {
  const nav = useNavigate()
  const loc = useLocation()
  const [user, setUser] = useState('')
  useEffect(() => {
    const u = localStorage.getItem('setx_user') || ''
    setUser(u)
  }, [])
  const logout = () => {
    localStorage.removeItem('setx_token')
    localStorage.removeItem('setx_user')
    nav('/login')
  }
  const isActive = (p: string) => loc.pathname === p || (p === '/' && loc.pathname.startsWith('/servers'))
  return (
    <div className="layout">
      <aside className="sidebar">
        <div className="brand"><span className="logo">⚡</span> Set-x</div>
        <nav>
          <a className={isActive('/') ? 'active' : ''} onClick={() => nav('/')}>📊 总览</a>
          <a className={loc.pathname.startsWith('/servers') ? 'active' : ''} onClick={() => nav('/servers')}>🖥️ 服务器</a>
          <a className={loc.pathname.startsWith('/nodes') ? 'active' : ''} onClick={() => nav('/nodes')}>🔗 节点</a>
          <a className={loc.pathname.startsWith('/users') ? 'active' : ''} onClick={() => nav('/users')}>👥 用户</a>
          <a className={loc.pathname.startsWith('/plans') ? 'active' : ''} onClick={() => nav('/plans')}>📦 套餐</a>
          <a className={loc.pathname.startsWith('/subscriptions') ? 'active' : ''} onClick={() => nav('/subscriptions')}>🔑 订阅</a>
        </nav>
        <div className="sidebar-footer">
          <div className="user-row"><span className="avatar">{(user[0] || 'A').toUpperCase()}</span><span>{user}</span></div>
          <button onClick={logout}>退出登录</button>
        </div>
      </aside>
      <main className="main">{children}</main>
    </div>
  )
}

export default function App() {
  return (
    <ToastProvider>
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/" element={
        <RequireAuth><Layout><Dashboard /></Layout></RequireAuth>
      } />
      <Route path="/servers" element={
        <RequireAuth><Layout><Servers /></Layout></RequireAuth>
      } />
      <Route path="/servers/:id" element={
        <RequireAuth><Layout><ServerDetail /></Layout></RequireAuth>
      } />
      <Route path="/users" element={
        <RequireAuth><Layout><Users /></Layout></RequireAuth>
      } />
      <Route path="/plans" element={
        <RequireAuth><Layout><Plans /></Layout></RequireAuth>
      } />
      <Route path="/nodes" element={
        <RequireAuth><Layout><Nodes /></Layout></RequireAuth>
      } />
      <Route path="/subscriptions" element={
        <RequireAuth><Layout><Subscriptions /></Layout></RequireAuth>
      } />
    </Routes>
    </ToastProvider>
  )
}

function RequireAuth({ children }: { children: React.ReactNode }) {
  if (!getToken()) return <Navigate to="/login" replace />
  return <>{children}</>
}
