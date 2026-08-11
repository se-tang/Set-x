import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../App'
import { useToast } from '../components/Toast'

const STATUS: Record<number, { text: string; cls: string }> = {
  0: { text: '在线', cls: 'online' },
  1: { text: '离线', cls: 'offline' },
  2: { text: '安装中', cls: 'installing' }
}

export default function Servers() {
  const [servers, setServers] = useState<any[]>([])
  const [showCreate, setShowCreate] = useState(false)
  const [view, setView] = useState<'form' | 'result'>('form')
  const [name, setName] = useState('')
  const [region, setRegion] = useState('')
  const [ip, setIp] = useState('')
  const [detecting, setDetecting] = useState(false)
  const [installCmd, setInstallCmd] = useState('')
  const [newServerId, setNewServerId] = useState<string | null>(null)
  const [serverOnline, setServerOnline] = useState(false)
  const [copied, setCopied] = useState(false)
  const nav = useNavigate()
  const { toast } = useToast()

  const load = () => api('/servers').then(setServers).catch(() => {})
  useEffect(() => { load() }, [])

  // 弹窗打开时重置状态
  useEffect(() => {
    if (showCreate) { setView('form'); setServerOnline(false); setInstallCmd(''); setNewServerId(null); setCopied(false); }
  }, [showCreate])

  // IP 失焦自动检测地区
  const detectRegion = async () => {
    if (!ip || ip.length < 7) return
    setDetecting(true)
    try {
      const res = await api(`/ip-lookup?ip=${encodeURIComponent(ip)}`)
      if (res.success && res.region) {
        setRegion(res.region)
        toast('info', `已识别地区：${res.region}`)
      }
    } catch { /* 静默降级——用户手动填 */ }
    setDetecting(false)
  }

  // 创建服务器 → 切 result 视图
  const create = async () => {
    if (!name || !ip) { toast('error', '名称和 IP 必填'); return }
    try {
      const res = await api('/servers', {
        method: 'POST',
        body: JSON.stringify({ name, region, ipAddress: ip })
      })
      if (res.install_command) setInstallCmd(res.install_command)
      setNewServerId(res.server?.id || null)
      setView('result')
      toast('success', '服务器已创建')
    } catch (e: any) {
      toast('error', e.message || '创建失败')
    }
  }

  // result 视图轮询服务器上线状态
  useEffect(() => {
    if (view !== 'result' || !newServerId) return
    const timer = setInterval(async () => {
      try {
        const list = await api('/servers')
        const s = list.find((x: any) => x.id === newServerId)
        if (s && (s.status === 0 || s.status === 'Online')) {
          setServerOnline(true)
          clearInterval(timer)
          toast('success', 'Agent 已连接上线！')
        }
      } catch { /* ignore */ }
    }, 2000)
    return () => clearInterval(timer)
  }, [view, newServerId])

  const copyCmd = async () => {
    try {
      await navigator.clipboard.writeText(installCmd)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch { toast('error', '复制失败，请手动选择复制') }
  }

  const del = async (id: string) => {
    if (!confirm('删除服务器及其所有节点？')) return
    await api(`/servers/${id}`, { method: 'DELETE' })
    toast('success', '已删除')
    load()
  }

  return (
    <div>
      <h1>服务器 <button className="primary btn-inline" onClick={() => setShowCreate(true)}>+ 新增</button></h1>
      <div className="page-sub">管理远程服务器与 Agent 部署</div>

      {showCreate && (
        <div className="modal">
          {view === 'form' ? (
            <div className="modal-card">
              <h3>新增服务器</h3>
              <input placeholder="名称（如 东京1）" value={name} onChange={(e) => setName(e.target.value)} />
              <input placeholder="IP 地址（输入后自动识别地区）" value={ip}
                onChange={(e) => setIp(e.target.value)} onBlur={detectRegion} />
              <div className="row region-row">
                <input placeholder="地区（如 JP）" value={region} onChange={(e) => setRegion(e.target.value)}
                  disabled={detecting} />
                {detecting && <span className="spinner-sm" />}
              </div>
              <div className="row" style={{ justifyContent: 'flex-end', marginTop: 8 }}>
                <button onClick={() => setShowCreate(false)}>取消</button>
                <button className="primary" onClick={create} disabled={detecting}>创建</button>
              </div>
            </div>
          ) : (
            <div className="modal-card">
              <h3>{serverOnline ? '🎉 Agent 已连接！' : '✅ 服务器已创建'}</h3>
              <p className="muted">请在目标机器执行以下命令（一次性令牌，30 分钟内有效）：</p>
              <div className="install-box">
                <pre>{installCmd}</pre>
                <button onClick={copyCmd}>{copied ? '✅ 已复制' : '📋 复制命令'}</button>
              </div>
              <div className={`deploy-status ${serverOnline ? 'ok' : 'waiting'}`}>
                {serverOnline
                  ? '🟢 Agent 已上线，节点管理已就绪'
                  : '⏳ 等待 Agent 连接...（弹窗自动检测）'}
              </div>
              <div className="row" style={{ justifyContent: 'flex-end', marginTop: 12 }}>
                <button onClick={() => setShowCreate(false)}>我知道了</button>
              </div>
            </div>
          )}
        </div>
      )}

      <div className="table-wrap">
        <table>
          <thead>
            <tr><th>名称</th><th>地区</th><th>IP</th><th>状态</th><th>最后在线</th><th>操作</th></tr>
          </thead>
          <tbody>
            {servers.length === 0 && (
              <tr><td colSpan={6} className="empty">暂无服务器——点击右上角「+ 新增」开始</td></tr>
            )}
            {servers.map((s: any) => (
              <tr key={s.id}>
                <td><a onClick={() => nav(`/servers/${s.id}`)}>{s.name}</a></td>
                <td>{s.region}</td>
                <td>{s.ipAddress}</td>
                <td><span className={`badge ${STATUS[s.status]?.cls || 'offline'}`}><span className="dot" />{STATUS[s.status]?.text || s.status}</span></td>
                <td>{s.lastSeenAt ? new Date(s.lastSeenAt).toLocaleString() : '-'}</td>
                <td><button className="danger small" onClick={() => del(s.id)}>删除</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
