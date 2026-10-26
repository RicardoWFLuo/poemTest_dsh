# 安全说明（SECURITY）

## ⚠️ 重要：本仓库历史提交中曾包含真实 API 密钥

`Assets/Scenes/RPG1.unity` 曾被明文提交过 **4 组真实可用密钥**：

| 用途 | 服务 | 密钥（截断显示） |
| --- | --- | --- |
| 诗词释义（通义千问 `qwen-turbo`） | 阿里云百炼 DashScope | `sk-1715…` |
| 诗词配图（通义万相 `wan2.6-t2i`） | 阿里云百炼 DashScope | `sk-4e99…` |
| 语音识别 API Key | 百度智能云 | `vmDfbcl…` |
| 语音识别 Secret Key | 百度智能云 | `O30JQ3…` |

> 表中只给出可识别的前缀，完整密钥请到对应控制台的密钥列表中查看。

### 当前处理状态

- ✅ **工作区已清除**：这 4 处明文已从 `Assets/Scenes/RPG1.unity` 中移除，改为运行时读取外部配置。
- ✅ **防止再次提交**：`.gitignore` 已加入 `Assets/Resources/api_keys.json`、`*.keystore`、`key.properties`。
- ❌ **Git 历史未改写**：这 4 组密钥**仍然存在于历史提交中**，任何克隆过本仓库的人都能取到。

---

## 必须执行的处理步骤

### 1. 吊销并重新生成密钥（最高优先级）

无论仓库是否公开，只要曾经推送过，就应当视为**已泄露**：

1. 登录 [阿里云百炼控制台](https://bailian.console.aliyun.com/) → API-KEY 管理 → **删除**上述两个 Key 并新建；
2. 登录 [百度智能云控制台](https://console.bce.baidu.com/) → 语音技术 → 应用管理 → **重置** API Key 与 Secret Key；
3. 检查账单与调用记录，确认没有异常调用。

### 2. 改写 Git 历史以彻底移除密钥

> 注意：改写历史会改变所有提交的哈希，需要强推并通知所有协作者重新克隆。

**方式一：git-filter-repo（推荐）**

```bash
cat > /tmp/replacements.txt <<'EOF'
regex:sk-[A-Za-z0-9]{32,}==>REDACTED
regex:(?i)(api_?key|secret_?key)\s*:\s*\S+==>REDACTED
EOF

git filter-repo --replace-text /tmp/replacements.txt
```

**方式二：BFG Repo-Cleaner**

```bash
java -jar bfg.jar --replace-text replacements.txt
git reflog expire --expire=now --all && git gc --prune=now --aggressive
```

**方式三：仓库不大时，直接重建历史**（删除 `.git` 后重新初始化并首次提交）也可接受。

改写完成后，在本地验证历史中已无残留：

```bash
git log --all -p | grep -nE "sk-[A-Za-z0-9]{20,}"   # 应无输出
```

### 3. 通知协作者

历史改写后，所有协作者需要重新克隆；直接 `git pull` 会产生冲突。

---

## 密钥的正确配置方式

项目**不包含任何密钥**，请通过以下任一方式提供：

### 方式 A：`Assets/Resources/api_keys.json`（推荐本地开发）

```bash
cp Assets/Resources/api_keys.example.json Assets/Resources/api_keys.json
```

```json
{
  "qwenTextKey": "阿里云百炼文本生成 API Key",
  "qwenImageKey": "阿里云百炼文生图 API Key",
  "baiduApiKey": "百度智能云语音识别 API Key",
  "baiduSecretKey": "百度智能云语音识别 Secret Key"
}
```

该文件已被 `.gitignore` 忽略。

### 方式 B：环境变量（推荐 CI / 多环境切换）

| 环境变量 | 对应密钥 |
| --- | --- |
| `POEMTEST_QWEN_TEXT_KEY` | 通义千问文本生成 |
| `POEMTEST_QWEN_IMAGE_KEY` | 通义万相文生图 |
| `POEMTEST_BAIDU_API_KEY` | 百度语音识别 API Key |
| `POEMTEST_BAIDU_SECRET_KEY` | 百度语音识别 Secret Key |

### 兜底方式

`PoemManager.textApiKey` / `imageApiKey`、`AudioSend.apiKey` / `secretKey` 仍保留为 Inspector
公开字段，仅在上述两种方式都取不到时使用。**请勿在 Inspector 中填入真实密钥后提交场景文件**——
历史泄露正是这样发生的。

读取逻辑见 [`Assets/Scripts/Config/ApiKeyConfig.cs`](Assets/Scripts/Config/ApiKeyConfig.cs)：
环境变量 → `api_keys.json` → Inspector 字段。

---

## 提交前自查

```bash
# 检查密钥特征
git grep -nE "sk-[A-Za-z0-9]{16,}"

# 检查是否误提交了本地密钥文件或证书
git status --short | grep -Ei "api_keys\.json|\.keystore|key\.properties"
```

建议将其配置为 `pre-commit` 钩子，或启用 Gitee/GitHub 的密钥扫描（Secret Scanning）功能。

---

## 关于客户端内置密钥的进一步建议

即使密钥没有进入 Git，**这类密钥最终仍会被打包进客户端**，任何拿到安装包的人都可以反编译提取。
如果本项目计划公开发布可执行包，建议进一步改造：

- 不直接调用 DashScope / 百度语音，改为**由自己的后端中转**，密钥只保存在服务端；
- 后端侧按用户 / 设备做调用频率限制与配额控制，防止密钥被滥用刷量；
- 对客户端下发短期有效的临时凭证（如百度 `access_token`），而非长期密钥。

---

## 报告安全问题

如果你在本项目中发现安全问题（包括但不限于密钥泄露、注入、越权调用），
请通过 **Issue（可选择私密）或仓库主页留下的联系方式** 告知，不要直接公开利用细节。
