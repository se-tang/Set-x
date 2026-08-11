import { useEffect, useState } from 'react'
import { QRCodeSVG } from 'qrcode.react'
import { api } from '../App'
import { useToast } from '../components/Toast'

export default function Subscriptions() {
  const [users, setUsers] = useState<any[]>([])
  const [qrUser, setQrUser] = useState<any>(null)
  const [qrUrl, setQrUrl] = useState('')
  const { toast } = useToast()

  const load = () => api('/users').then(setUsers).catch(() => {})
  useEffect(() => { load() }, [])

  const showQr = async (u: any) => {
    const info = await api(`/users/${u.id}/subscription-info`)
    if (info.success) {
      setQrUser(u)
      setQrUrl(info.subscription_url)
    } else {
      toast('error', info.message || '获取订阅信息失败')
    }
  }

  const resetToken = async (u: any) => {
    if (!confirm(`重置 ${u.username} 的订阅 Token？旧链接将立即失效`)) return
    const res = await api(`/users/${u.id}/subscription-token/reset`, { method: 'POST' })
    if (res.success) { toast('success', 'Token 已重置'); load() }
  }

  const copy = async (text: string, label: string) => {
    try {
      await navigator.clipboard.writeText(text)
      toast('success', `${label}已复制`)
    } catch { toast('error', '复制失败') }
  }

  const mask = (token: string) => token ? `${token.slice(0, 4)}***${token.slice(-2)}` : '-'

  return (
    <div>
      <h1>订阅管理</h1>
      <div className="page-sub">管理用户订阅链接、二维码与 Token</div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr><th>用户名</th><th>订阅 Token</th><th>订阅链接</th><th>操作</th></tr>
          </thead>
          <tbody>
            {users.length === 0 && <tr><td colSpan={4} className="empty">暂无用户</td></tr>}
            {users.map((u: any) => (
              <tr key={u.id}>
                <td><strong>{u.username}</strong>{u.disabled && <span className="badge stop" style={{ marginLeft: 8 }}>禁用</span>}</td>
                <td><code className="sub-token">{mask(u.subscriptionToken)}</code></td>
                <td className="sub-link-cell">
                  <code className="sub-link">{`${location.origin}/sub/${u.subscriptionToken}`}</code>
                  <button className="small" onClick={() => copy(`${location.origin}/sub/${u.subscriptionToken}`, '订阅链接')}>复制</button>
                </td>
                <td>
                  <button className="small" onClick={() => showQr(u)}>二维码</button>
                  <button className="small" onClick={() => resetToken(u)}>重置 Token</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {qrUser && (
        <div className="modal">
          <div className="modal-card qr-card">
            <h3>{qrUser.username} 的订阅二维码</h3>
            <div className="qr-box">
              <QRCodeSVG value={qrUrl} size={200} level="M" />
            </div>
            <p className="muted qr-url">{qrUrl}</p>
            <div className="row" style={{ justifyContent: 'center', gap: 8, marginTop: 12 }}>
              <button className="primary" onClick={() => copy(qrUrl, '链接')}>复制链接</button>
              <button onClick={() => setQrUser(null)}>关闭</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
