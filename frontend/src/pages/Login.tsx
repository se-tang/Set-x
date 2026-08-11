import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../App'

export default function Login() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const nav = useNavigate()

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      const res = await api('/auth/login', {
        method: 'POST',
        body: JSON.stringify({ username, password })
      })
      if (res.token) {
        localStorage.setItem('setx_token', res.token)
        localStorage.setItem('setx_user', res.username)
        nav('/')
      } else {
        setError(res.message || '登录失败')
      }
    } catch (err: any) {
      setError(err.message || '登录失败')
    }
  }

  return (
    <div className="login-wrap">
      <form className="login-card" onSubmit={submit}>
        <div className="logo-big">⚡</div>
        <h1>Set-x 主控</h1>
        <div className="login-sub">Xray 多服务器管理与订阅系统</div>
        <input placeholder="用户名" value={username}
          onChange={(e) => setUsername(e.target.value)} />
        <input placeholder="密码" type="password" value={password}
          onChange={(e) => setPassword(e.target.value)} />
        {error && <div className="error">{error}</div>}
        <button type="submit">登 录</button>
      </form>
    </div>
  )
}
