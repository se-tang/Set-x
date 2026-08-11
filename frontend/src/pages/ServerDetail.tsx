import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { api } from '../App'

export default function ServerDetail() {
  const { id } = useParams()
  const [data, setData] = useState<any>(null)
  const [showNode, setShowNode] = useState(false)
  const [nodeName, setNodeName] = useState('')
  const [nodePort, setNodePort] = useState('443')
  const [nodeProtocol, setNodeProtocol] = useState('VLESS')
  const [nodeConfig, setNodeConfig] = useState('{}')

  const load = () => api(`/servers/${id}/status`).then(setData).catch(() => {})
  useEffect(() => { load() }, [id])

  const addNode = async () => {
    await api(`/servers/${id}/nodes`, {
      method: 'POST',
      body: JSON.stringify({
        name: nodeName, port: parseInt(nodePort),
        protocol: nodeProtocol, configJson: nodeConfig
      })
    })
    setShowNode(false)
    setNodeName(''); setNodeConfig('{}')
    load()
  }

  const delNode = async (nodeId: string) => {
    if (!confirm('删除节点？')) return
    await api(`/servers/nodes/${nodeId}`, { method: 'DELETE' })
    load()
  }

  if (!data) return <div>加载中...</div>

  return (
    <div>
      <h1>{data.server?.name} <span className="muted">({data.server?.ipAddress})</span></h1>
      <button className="primary" onClick={() => setShowNode(true)}>+ 新建节点</button>

      {showNode && (
        <div className="modal">
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
            <div className="row">
              <button className="primary" onClick={addNode}>创建</button>
              <button onClick={() => setShowNode(false)}>取消</button>
            </div>
          </div>
        </div>
      )}

      <table>
        <thead>
          <tr><th>名称</th><th>协议</th><th>端口</th><th>状态</th><th>操作</th></tr>
        </thead>
        <tbody>
          {(data.nodes || []).map((n: any) => (
            <tr key={n.id}>
              <td>{n.name}</td>
              <td>{n.protocol}</td>
              <td>{n.port}</td>
              <td>{n.enabled ? '✅ 启用' : '⛔ 停用'}</td>
              <td><button className="danger" onClick={() => delNode(n.id)}>删除</button></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
