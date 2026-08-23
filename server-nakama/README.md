# Nakama Server Module

`src/rules` 是纯规则核心；`src/matches` 和 `src/rpc` 是 Nakama 边界适配器。构建产物只有 `build/index.js`，供 Docker 容器加载。

当前仅实现工程闭环需要的开局、结束回合和认输。它不是完整玩法实现。新增规则时先写规则测试，再接 Match Handler，避免把领域逻辑写进网络回调。

运行：

```powershell
npm test
npm run build
```
