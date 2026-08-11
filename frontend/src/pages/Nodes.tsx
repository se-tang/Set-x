import { useEffect, useState } from 'react'
import { api } from '../App'
import { useToast } from '../components/Toast'

const DEPLOY: Record<number, { text: string; cls: string }> = {
  0: { text: '待部署', cls: 'pending' },
  1: { text: '下发中', cls: 'applying' },
  2: { text: '已部署', cls: 'online' },
  3: { text: '失败', cls: 'offline' }
}
const PROTOCOLS = ['VLESS', 'VMess', 'Trojan', 'Shadowsocks', 'Hysteria2', 'AnyTLS']

export default function Nodes() {
  const [nodes, setNodes] = useState<any[]>([])
  const [servers, setServers] = useState<any[]>([])
  const [filterServer, setFilterServer] = useState('')
  const [filterProto, setFilterProto] = useState('')
  const [filterStatus, setFilterStatus] = useState('')
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const { toast } = useToast()

  const load = () => {
    const params = new URLSearchParams()
    if (filterServer) params.set('serverId', filterServer)
    if (filterProto) params.set('protocol', filterProto)
    if (filterStatus) params.set('deployStatus', filterStatus)
    api(`/nodes?${params}`).then(setNodes).catch(() => {})
  }
  useEffect(() => { load(); api('/servers').then(setServers).catch(() => {}) }, [filterServer, filterProto, filterStatus])

  const toggleSelect = (id: string) => {
    setSelected(prev => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })
  }

  const batchEnabled = async (enabled: boolean) => {
    if (!selected.size) { toast('error', '未选择节点'); return }
    await api('/nodes/batch-enabled', { method: 'POST', body: JSON.stringify({ nodeIds: [...selected], enabled }) })
    toast('success', `已${enabled ? '启用' : '禁用'} ${selected.size} 个节点`)
    setSelected(new Set()); load()
  }

  const batchDelete = async () => {
    if (!selected.size) { toast('error', '未选择节点'); return }
    if (!confirm(`删除 ${selected.size} 个节点？`)) return
    await api('/nodes/batch-delete', { method: 'POST', body: JSON.stringify({ nodeIds: [...selected] }) })
    toast('success', '已批量删除')
    setSelected(new Set()); load()
  }

  const redeploy = async (n: any) => {
    await api(`/servers/nodes/${n.id}`, {
      method: 'PATCH',
      body: JSON.stringify({ name: n.name, protocol: n.protocol, port: n.port, configJson: n.configJson, enabled: true, rateMultiplier: n.rateMultiplier || 1 })
    })
    toast('info', `重新下发 ${n.name}...`)
    load()
  }

  return (
    <div>
      <h1>节点管理</h1>
      <div className="page-sub">全局节点视图——批量管理与部署状态监控</div>

      <div className="filter-bar">
        <select value={filterServer} onChange={(e) => setFilterServer(e.target.value)}>
          <option value="">全部服务器</option>
          {servers.map((s: any) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </select>
        <select value={filterProto} onChange={(e) => setFilterProto(e.target.value)}>
          <option value="">全部协议</option>
          {PROTOCOLS.map(p => <option key={p}>{p}</option>)}
        </select>
        <select value={filterStatus} onChange={(e) => setFilterStatus(e.target.value)}>
          <option value="">全部状态</option>
          <option value="2">已部署</option>
          <option value="1">下发中</option>
          <option value="3">失败</option>
          <option value="0">待部署</option>
        </select>
        {selected.size > 0 && (
          <span className="batch-actions">
            <button className="small" onClick={() => batchEnabled(true)}>启用({selected.size})</button>
            <button className="small" onClick={() => batchEnabled(false)}>禁用({selected.size})</button>
            <button className="danger small" onClick={batchDelete}>删除({selected.size})</button>
          </span>
        )}
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr><th style={{ width: 36 }}></th><th>名称</th><th>服务器</th><th>协议</th><th>端口</th><th>部署状态</th><th>绑定用户</th><th>启用</th><th>操作</th></tr>
          </thead>
          <tbody>
            {nodes.length === 0 && <tr><td colSpan={9} className="empty">暂无节点</td></tr>}
            {nodes.map((n: any) => (
              <tr key={n.id}>
                <td><input type="checkbox" checked={selected.has(n.id)} onChange={() => toggleSelect(n.id)} /></td>
                <td><strong>{n.name}</strong>{n.deployError && <span className="deploy-err-text" title={n.deployError}>⚠️</span>}</td>
                <td>{n.serverName}</td>
                <td>{n.protocol}</td>
                <td>{n.port}</td>
                <td><span className={`badge ${DEPLOY[n.deployStatus]?.cls || 'pending'}`}>{DEPLOY[n.deployStatus]?.text || '待部署'}</span></td>
                <td>{n.bindUserCount}</td>
                <td>{n.enabled ? '✅' : '⛔'}</td>
                <td><button className="small" onClick={() => redeploy(n)}>重新下发</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
