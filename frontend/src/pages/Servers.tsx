import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../App'

const STATUS: Record<number, string> = { 0: '🟢 在线', 1: '🔴 离线', 2: '🟡 安装中' }

export default function Servers() {
  const [servers, setServers] = useState<any[]>([])
  const [showCreate, setShowCreate] = useState(false)
  const [name, setName] = useState('')
  const [region, setRegion] = useState('')
  const [ip, setIp] = useState('')
  const [installCmd, setInstallCmd] = useState('')
  const nav = useNavigate()

  const load = () => api('/servers').then(setServers).catch(() => {})
  useEffect(() => { load() }, [])

  const create = async () => {
    const res = await api('/servers', {
      method: 'POST',
      body: JSON.stringify({ name, region, ipAddress: ip })
    })
    if (res.install_command) setInstallCmd(res.install_command)
    setShowCreate(false)
    load()
  }

  const del = async (id: string) => {
    if (!confirm('删除服务器及其所有节点？')) return
    await api(`/servers/${id}`, { method: 'DELETE' })
    load()
  }

  return (
    <div>
      <h1>服务器 <button className="primary" onClick={() => setShowCreate(true)}>+ 新增</button></h1>

      {showCreate && (
        <div className="modal">
          <div className="modal-card">
            <h3>新增服务器</h3>
            <input placeholder="名称（如 东京1）" value={name} onChange={(e) => setName(e.target.value)} />
            <input placeholder="地区（如 JP）" value={region} onChange={(e) => setRegion(e.target.value)} />
            <input placeholder="IP 地址" value={ip} onChange={(e) => setIp(e.target.value)} />
            <div className="row">
              <button className="primary" onClick={create}>创建</button>
              <button onClick={() => setShowCreate(false)}>取消</button>
            </div>
          </div>
        </div>
      )}

      {installCmd && (
        <div className="install-box">
          <h3>📋 安装命令（一次性令牌，30 分钟有效）</h3>
          <pre>{installCmd}</pre>
          <button onClick={() => navigator.clipboard.writeText(installCmd)}>复制</button>
          <button onClick={() => setInstallCmd('')}>关闭</button>
        </div>
      )}

      <table>
        <thead>
          <tr><th>名称</th><th>地区</th><th>IP</th><th>状态</th><th>最后在线</th><th>操作</th></tr>
        </thead>
        <tbody>
          {servers.map((s: any) => (
            <tr key={s.id}>
              <td><a onClick={() => nav(`/servers/${s.id}`)}>{s.name}</a></td>
              <td>{s.region}</td>
              <td>{s.ipAddress}</td>
              <td>{STATUS[s.status] || s.status}</td>
              <td>{s.lastSeenAt ? new Date(s.lastSeenAt).toLocaleString() : '-'}</td>
              <td><button className="danger" onClick={() => del(s.id)}>删除</button></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
