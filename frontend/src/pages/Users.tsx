import { useEffect, useState } from 'react'
import { api } from '../App'

export default function Users() {
  const [users, setUsers] = useState<any[]>([])
  const [plans, setPlans] = useState<any[]>([])
  const [showCreate, setShowCreate] = useState(false)
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [bindPlan, setBindPlan] = useState<Record<string, string>>({})

  const load = () => { api('/users').then(setUsers).catch(() => {}); api('/plans').then(setPlans).catch(() => {}) }
  useEffect(() => { load() }, [])

  const create = async () => {
    await api('/auth/register', {
      method: 'POST',
      body: JSON.stringify({ username, password })
    })
    setShowCreate(false)
    setUsername(''); setPassword('')
    load()
  }

  const toggleDisable = async (u: any) => {
    await api(`/users/${u.id}`, {
      method: 'PATCH',
      body: JSON.stringify({ disabled: !u.disabled })
    })
    load()
  }

  const bind = async (userId: string) => {
    const planId = bindPlan[userId]
    if (!planId) return
    await api(`/users/${userId}/plans`, {
      method: 'POST',
      body: JSON.stringify({ planId })
    })
    load()
  }

  const copySub = (token: string) => {
    navigator.clipboard.writeText(`${location.origin}/sub/${token}`)
  }

  return (
    <div>
      <h1>用户 <button className="primary" onClick={() => setShowCreate(true)}>+ 新建</button></h1>

      {showCreate && (
        <div className="modal">
          <div className="modal-card">
            <h3>新建用户</h3>
            <input placeholder="用户名" value={username} onChange={(e) => setUsername(e.target.value)} />
            <input placeholder="密码" value={password} onChange={(e) => setPassword(e.target.value)} />
            <div className="row">
              <button className="primary" onClick={create}>创建</button>
              <button onClick={() => setShowCreate(false)}>取消</button>
            </div>
          </div>
        </div>
      )}

      <table>
        <thead>
          <tr><th>用户名</th><th>订阅链接</th><th>状态</th><th>绑定套餐</th><th>操作</th></tr>
        </thead>
        <tbody>
          {users.map((u: any) => (
            <tr key={u.id}>
              <td>{u.username}{u.role === 'Admin' && ' ⭐'}</td>
              <td>
                <code className="sub-token">{u.subscriptionToken}</code>
                <button onClick={() => copySub(u.subscriptionToken)}>复制</button>
              </td>
              <td>{u.disabled ? '⛔ 禁用' : '✅ 正常'}</td>
              <td>
                <div className="row">
                  <select value={bindPlan[u.id] || ''} onChange={(e) => setBindPlan({ ...bindPlan, [u.id]: e.target.value })}>
                    <option value="">选择套餐</option>
                    {plans.map((p: any) => <option key={p.id} value={p.id}>{p.name}</option>)}
                  </select>
                  <button onClick={() => bind(u.id)}>绑定</button>
                </div>
              </td>
              <td>
                <button onClick={() => toggleDisable(u)}>{u.disabled ? '启用' : '禁用'}</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
