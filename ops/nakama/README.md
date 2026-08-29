# Local Nakama

根目录的 `docker-compose.yml` 用于本机开发：

- HTTP/API：`http://127.0.0.1:17350`（容器内 `7350`）
- 控制台：`http://127.0.0.1:17351`（容器内 `7351`）
- gRPC：`127.0.0.1:17349`（容器内 `7349`）
- TypeScript 模块：只读挂载 `server-nakama/build/`

宿主端口使用 `17349–17351`，避免 Windows Docker Desktop / Hyper-V 常见的 `73xx` 动态保留段冲突。先执行 `npm run build`。首次启动前将 `.env.example` 复制为 `.env` 并更换本地密码。此 Compose 文件不是生产部署模板；生产环境必须使用密钥管理、TLS、备份和受限网络。
