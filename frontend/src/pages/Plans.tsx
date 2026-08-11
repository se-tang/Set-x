import { useEffect, useState } from 'react'
import { api } from '../App'

export default function Plans() {
  const [plans, setPlans] = useState<any[]>([])
  const [showCreate, setShowCreate] = useState(false)
  const [name, setName] = useState('')
  const [limitGb, setLimitGb] = useState('100')
  const [speed, setSpeed] = useState('0')
  const [days, setDays] = useState('30')
  const [price, setPrice] = useState('0')

  const load = () => api('/plans').then(setPlans).catch(() => {})
  useEffect(() => { load() }, [])

  const create = async () => {
    await api('/plans', {
      method: 'POST',
      body: JSON.stringify({
        name,
        trafficLimitBytes: Math.round(parseFloat(limitGb) * 1024 * 1024 * 1024),
        speedLimitMbps: parseInt(speed),
        durationDays: parseInt(days),
        price: parseFloat(price)
      })
    })
    setShowCreate(false)
    load()
  }

  const del = async (id: string) => {
    if (!confirm('删除套餐？')) return
    await api(`/plans/${id}`, { method: 'DELETE' })
    load()
  }

  const fmt = (b: number) => {
    if (!b) return '不限'
    return `${(b / 1024 / 1024 / 1024).toFixed(0)} GB`
  }

  return (
    <div>
      <h1>套餐 <button className="primary" onClick={() => setShowCreate(true)}>+ 新建</button></h1>

      {showCreate && (
        <div className="modal">
          <div className="modal-card">
            <h3>新建套餐</h3>
            <input placeholder="名称" value={name} onChange={(e) => setName(e.target.value)} />
            <div className="row">
              <input placeholder="流量(GB)" value={limitGb} onChange={(e) => setLimitGb(e.target.value)} />
              <input placeholder="限速(Mbps,0=不限)" value={speed} onChange={(e) => setSpeed(e.target.value)} />
            </div>
            <div className="row">
              <input placeholder="时长(天)" value={days} onChange={(e) => setDays(e.target.value)} />
              <input placeholder="价格" value={price} onChange={(e) => setPrice(e.target.value)} />
            </div>
            <div className="row">
              <button className="primary" onClick={create}>创建</button>
              <button onClick={() => setShowCreate(false)}>取消</button>
            </div>
          </div>
        </div>
      )}

      <table>
        <thead>
          <tr><th>名称</th><th>流量</th><th>限速</th><th>时长</th><th>价格</th><th>操作</th></tr>
        </thead>
        <tbody>
          {plans.map((p: any) => (
            <tr key={p.id}>
              <td>{p.name}</td>
              <td>{fmt(p.trafficLimitBytes)}</td>
              <td>{p.speedLimitMbps ? `${p.speedLimitMbps} Mbps` : '不限'}</td>
              <td>{p.durationDays} 天</td>
              <td>¥{p.price}</td>
              <td><button className="danger" onClick={() => del(p.id)}>删除</button></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
