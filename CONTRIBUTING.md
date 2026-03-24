# 贡献指南 (Contributing Guide)

感谢你对本项目的关注！欢迎提交 Pull Request 参与贡献。

## 贡献流程

### 1. Fork 仓库

点击页面右上角的 **Fork** 按钮，将本仓库 Fork 到你自己的 GitHub 账号下。

### 2. Clone 你的 Fork

```bash
git clone https://github.com/<你的用户名>/Software_client.git
cd Software_client
```

### 3. 创建功能分支

**请勿直接在 `dev` 分支上提交代码。** 从 `dev` 分支创建新的功能/修复分支：

```bash
git checkout dev
git pull origin dev
git checkout -b feature/your-feature-name
```

分支命名规范：
- 新功能：`feature/xxx`
- Bug 修复：`fix/xxx`
- 文档更新：`docs/xxx`
- 重构：`refactor/xxx`

### 4. 提交代码

遵守本项目的代码风格（参见 `.editorconfig`）并提交你的修改：

```bash
git add .
git commit -m "feat: 简短描述你的改动"
git push origin feature/your-feature-name
```

提交信息规范请参考 [`.gitmessage.txt`](.gitmessage.txt)。

### 5. 发起 Pull Request

1. 访问你 Fork 的仓库页面
2. 点击 **Compare & pull request**
3. 确认目标分支为 `dev`（**不要** 直接 PR 到 `main`）
4. 填写 PR 描述，说明改动内容和原因
5. 提交 PR，等待 Review

## 重要规则

- **禁止直接 push 到 `dev` 分支**，所有代码必须通过 Pull Request 合并
- PR 必须通过 CI 检查才能合并
- PR 需要至少 1 位 Reviewer 审批后方可合并

## 代码规范

本项目为 Unity (C#) 项目，请遵守以下规范：

- 严格遵守 `.editorconfig` 中的代码风格要求
- 不在 Update/热路径中使用 LINQ 或产生 GC 分配
- 遵守 Unity 生命周期规范（Awake/OnEnable/Start/OnDisable/OnDestroy）
- 缓存 `GetComponent` 等开销较大的 API 调用结果

## 问题反馈

如有 Bug 或功能建议，请先通过 [Issues](../../issues) 提交，避免重复工作。
