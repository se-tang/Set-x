import { useEffect, useState } from 'react'
import { api } from '../App'

export default function Dashboard() {
  const [servers, setServers] = useState<any[]>([])
  const [users, setUsers] = useState<any[]>([])
  const [plans, setPlans] = useState<any[]>([])
  const [trend, setTrend] = useState<any[]>([])
  const [traffic, setTraffic] = useState<any>(null)

  useEffect(() => {
    api('/servers').then(setServers).catch(() => {})
    api('/users').then(setUsers).catch(() => {})
    api('/plans').then(setPlans).catch(() => {})
    api('/traffic/summary').then(setTraffic).catch(() => {})
    api('/traffic/trend').then(setTrend).catch(() => {})
  }, [])

  const online = servers.filter((s: any) => s.status === 0).length
  const fmt = (b: number) => {
    if (!b) return '0 B'
    const units = ['B', 'KB', 'MB', 'GB', 'TB']
    let i = 0
    while (b >= 1024 && i < units.length - 1) { b /= 1024; i++ }
    return `${b.toFixed(1)} ${units[i]}`
  }

  return (
    <div>
      <h1>总览</h1>
      <div className="cards">
        <div className="card"><div className="num">{servers.length}</div><div>服务器（在线 {online}）</div></div>
        <div className="card"><div className="num">{users.length}</div><div>用户</div></div>
        <div className="card"><div className="num">{plans.length}</div><div>套餐</div></div>
        <div className="card"><div className="num">{fmt(traffic?.total || 0)}</div><div>今日流量</div></div>
      </div>
      <h2>30 天流量趋势</h2>
      <div className="trend">
        {trend.length === 0 && <p className="muted">暂无数据（Agent 上报后显示）</p>}
        {trend.map((t: any) => (
          <div key={t.date} className="trend-bar" title={`${t.date}: ${fmt(t.download + t.upload)}`}>
            <div className="bar" style={{ height: `${Math.min(100, Math.log10((t.download + t.upload) / 1024 + 1) * 25)}%` }} />
            <span>{t.date.slice(5)}</span>
          </div>
        ))}
      </div>
    </div>
  )
}
