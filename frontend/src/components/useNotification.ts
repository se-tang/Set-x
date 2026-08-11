import { useEffect, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import { getToken } from '../App'

/**
 * 前端通知连接（NotificationHub，JWT 鉴权）
 * 监听 NodeDeployStatusChanged：节点部署状态回执
 */
export function useNotification(onNodeDeploy: (nodeId: string, success: boolean, error?: string) => void) {
  const cbRef = useRef(onNodeDeploy)
  cbRef.current = onNodeDeploy

  useEffect(() => {
    const token = getToken()
    if (!token) return
    let stopped = false

    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notify', { accessTokenFactory: () => getToken() || '' })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 15000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conn.on('NodeDeployStatusChanged', (nodeId: string, success: boolean, error?: string) => {
      if (!stopped) cbRef.current(nodeId, success, error)
    })

    conn.start().catch(() => { /* 失败自动重连 */ })

    return () => { stopped = true; conn.stop().catch(() => {}) }
  }, [])
}
