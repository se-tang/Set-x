import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { api } from '../App'
import { useToast } from '../components/Toast'
import { useNotification } from '../components/useNotification'

const DEPLOY: Record<number, { text: string; cls: string }> = {
  0: { text: '待部署', cls: 'pending' },
  1: { text: '下发中', cls: 'applying' },
  2: { text: '已部署', cls: 'online' },
  3: { text: '失败', cls: 'offline' }
}

export default function ServerDetail() {
  const { id } = useParams()
  const [data, setData] = useState<any>(null)
  const [showNode, setShowNode] = useState(false)
  const [nodePhase, setNodePhase] = useState<'form' | 'deploying' | 'done'>('form')
  const [deployOk, setDeployOk] = useState(false)
  const [deployError, setDeployError] = useState('')
  const [pendingNodeId, setPendingNodeId] = useState<string | null>(null)
  const [nodeName, setNodeName] = useState('')
  const [nodePort, setNodePort] = useState('443')
  const [nodeProtocol, setNodeProtocol] = useState('VLESS')
  const [nodeConfig, setNodeConfig] = useState('{}')
  const { toast } = useToast()

  const load = () => api(`/servers/${id}/status`).then(setData).catch(() => {})
  useEffect(() => { load() }, [id])

  // 监听部署回执
  useNotification((nodeId, success, error) => {
    if (nodeId === pendingNodeId) {
      setDeployOk(success)
      setDeployError(error || '')
      setNodePhase('done')
      load()
    }
  })

  // 打开弹窗重置
  useEffect(() => {
    if (showNode) { setNodePhase('form'); setDeployOk(false); setDeployError(''); setPendingNodeId(null); }
  }, [showNode])

  // 15 秒超时兜底
  useEffect(() => {
    if (nodePhase !== 'deploying') return
    const t = setTimeout(() => {
      setDeployOk(false)
      setDeployError('服务器未响应，请检查该服务器是否在线')
      setNodePhase('done')
    }, 15000)
    return () => clearTimeout(t)
  }, [nodePhase])

  const addNode = async () => {
    try {
      const res = await api(`/servers/${id}/nodes`, {
        method: 'POST',
        body: JSON.stringify({
          name: nodeName, port: parseInt(nodePort),
          protocol: nodeProtocol, configJson: nodeConfig
        })
      })
      setPendingNodeId(res.id)
      setNodePhase('deploying')
      toast('info', '正在下发配置...')
    } catch (e: any) {
      toast('error', e.message || '创建失败')
    }
  }

  const redeploy = async (n: any) => {
    await api(`/servers/nodes/${n.id}`, {
      method: 'PATCH',
      body: JSON.stringify({
        name: n.name, protocol: n.protocol, port: n.port,
        configJson: n.configJson, enabled: true, rateMultiplier: n.rateMultiplier || 1
      })
    })
    toast('info', `正在重新下发 ${n.name}...`)
    load()
  }

  const delNode = async (nodeId: string) => {
    if (!confirm('删除节点？')) return
    await api(`/servers/nodes/${nodeId}`, { method: 'DELETE' })
    toast('success', '已删除')
    load()
  }

  const copySub = (configJson: string) => {
    const cfg = JSON.parse(configJson || '{}')
    const uuid = cfg.uuid || ''
    const host = data?.server?.ipAddress || ''
    const link = `vless://${uuid}@${host}:${nodePort}?encryption=none&type=${cfg.network || 'tcp'}${cfg.path ? `&path=${encodeURIComponent(cfg.path)}` : ''}`
    navigator.clipboard.writeText(link)
    toast('success', '已复制订阅链接')
  }

  if (!data) return <div className="loading">加载中</div>

  return (
    <div>
      <h1>{data.server?.name} <span className="muted">({data.server?.ipAddress})</span></h1>
      <div className="page-sub">管理该服务器上的节点与部署状态</div>
      <button className="primary" onClick={() => setShowNode(true)}>+ 新建节点</button>

      {showNode && (
        <div className="modal">
          {nodePhase === 'form' && (
            <div className="modal-card">
              <h3>新建节点</h3>
              <input placeholder="节点名称" value={nodeName} onChange={(e) => setNodeName(e.target.value)} />
              <div className="row">
                <input placeholder="端口" value={nodePort} onChange={(e) => setNodePort(e.target.value)} />
                <select value={nodeProtocol} onChange={(e) => setNodeProtocol(e.target.value)}>
                  {['VLESS', 'VMess', 'Trojan', 'Shadowsocks', 'Hysteria2', 'AnyTLS'].map(p => <option key={p}>{p}</option>)}
                </select>
              </div>
              <textarea placeholder={'配置 JSON（如 {"network":"ws","path":"/","tls":"true","sni":"example.com"}）'}
                value={nodeConfig} onChange={(e) => setNodeConfig(e.target.value)} rows={4} />
              <div className="row" style={{ justifyContent: 'flex-end' }}>
                <button onClick={() => setShowNode(false)}>取消</button>
                <button className="primary" onClick={addNode}>创建并下发</button>
              </div>
            </div>
          )}

          {nodePhase === 'deploying' && (
            <div className="modal-card deploy-card">
              <h3>🔄 正在下发配置...</h3>
              <p className="muted">等待 Agent 校验并重启 Xray（通常几秒）</p>
              <div className="deploy-spinner" />
            </div>
          )}

          {nodePhase === 'done' && (
            <div className="modal-card deploy-card">
              {deployOk ? (
                <>
                  <h3>✅ 节点已成功部署</h3>
                  <p className="muted">端口 {nodePort} 已监听，订阅已生效</p>
                </>
              ) : (
                <>
                  <h3>❌ 部署失败</h3>
                  <div className="deploy-error">{deployError || '未知错误'}</div>
                </>
              )}
              <div className="row" style={{ justifyContent: 'flex-end', marginTop: 14 }}>
                {!deployOk && <button onClick={() => { setNodePhase('form') }}>返回修改</button>}
                <button className="primary" onClick={() => { setShowNode(false); load() }}>关闭</button>
              </div>
            </div>
          )}
        </div>
      )}

      <div className="table-wrap" style={{ marginTop: 16 }}>
        <table>
          <thead>
            <tr><th>名称</th><th>协议</th><th>端口</th><th>部署状态</th><th>启用</th><th>操作</th></tr>
          </thead>
          <tbody>
            {(data.nodes || []).map((n: any) => (
              <tr key={n.id}>
                <td><strong>{n.name}</strong></td>
                <td>{n.protocol}</td>
                <td>{n.port}</td>
                <td>
                  <span className={`badge ${DEPLOY[n.deployStatus]?.cls || 'pending'}`}>{DEPLOY[n.deployStatus]?.text || '待部署'}</span>
                  {n.deployError && <span className="deploy-err-text" title={n.deployError}>⚠️</span>}
                </td>
                <td>{n.enabled ? '✅' : '⛔'}</td>
                <td>
                  <button className="small" onClick={() => redeploy(n)}>重新下发</button>
                  <button className="danger small" onClick={() => delNode(n.id)}>删除</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
