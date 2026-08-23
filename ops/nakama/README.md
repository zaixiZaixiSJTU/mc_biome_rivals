# Local Nakama

根目录的 `docker-compose.yml` 用于本机开发：

- HTTP/API：`http://127.0.0.1:7350`
- 控制台：`http://127.0.0.1:7351`
- TypeScript 模块：只读挂载 `server-nakama/build/`

先执行 `npm run build`。首次启动前将 `.env.example` 复制为 `.env` 并更换本地密码。此 Compose 文件不是生产部署模板；生产环境必须使用密钥管理、TLS、备份和受限网络。
