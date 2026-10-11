# Light In Dark 握手验证系统

模组与服务器配合的**签名验证握手**，用于在大厅中检查签名票据、构建版本及挑战应答是否一致。

## 原理

```
房主 ──Challenge RPC(一次性 nonce)──▶ 玩家
玩家 ──POST /verify(version+hash×2+accountId+nonce)──▶ 验证服务器
服务器 ──合法则 ECDSA P-256 私钥签名票据──▶ 玩家
玩家 ──Handshake RPC(上报 hash×2 + 票据)──▶ 房主
房主验签(内置公钥) + nonce 匹配 + hash 与本地一致 + accountId 匹配
  └─ 任一失败 → 原生右下角提示 / 延迟踢出
```

**验证范围**：未修改的客户端会计算文件 hash，服务器只为 `official.json` 中登记的构建签票；房主检查签名、过期时间、挑战 nonce、版本、hash 及可用的账号身份。持有私钥的一方才能伪造签名。

**已知边界**：`/verify` 接收客户端自报的 hash、账号和 nonce，无法证明远端实际加载的 DLL；修改过的客户端可上报已登记 hash 并申请一致票据。因此该协议不能作为远程完整性证明。账号身份为空时也不能提供账号绑定保证。服务器不可达或拒签时不发握手，由房主按超时（默认 10 秒）判定。

---

## 一、部署验证服务器

### 1. 生成密钥对(首次运行自动生成)
```bash
dotnet run -c Release --project VerificationServer --urls http://0.0.0.0:58080
```
首次运行会在 `bin/Release/net10.0/data/` 生成:
- `private.pem` — **私钥,绝对不要泄露,不要放进模组**
- `public.pem` — 公钥,供编译进模组
- `official.json` — 官方 hash 白名单（初始为空，登记前拒绝所有构建）

### 2. 写入官方 hash(每次发版必做!)
1. 构建出 `Light.dll` 与 `LightInDark.dll` 后,计算两者的 hash(见下"计算 hash")
2. 编辑 `data/official.json`:
```json
{
  "versions": [
    { "version": "0.0.1", "apiHash": -871552443, "modHash": 1180251441 }
  ]
}
```
构建 hash 未登记会导致服务器拒签，房主最终提示握手超时；使用已登记的其他构建应答时，房主提示版本不匹配。

### 3. 部署到服务器
- 发布: `dotnet publish -c Release`
- 建议用 Nginx/反代加 HTTPS(票据内容会签名但明文传输,HTTPS 防窃听/中间人)
- `data/` 目录持久化(密钥、official.json 都在里面),重启不丢失
- 健康检查: `GET /health`

### 4. 把公钥写进模组
`GET <服务器>/pubkey` 返回 PEM,替换
`LightAPI/Handshake/HandshakeCrypto.cs` 里的 `PublicKeyPem` 常量,重新构建。

---

## 二、计算 hash(发版脚本)

hash = 文件 SHA-256 前 4 字节转 int32(与模组 `HandshakeCrypto.ComputeFileHash` 一致)。

```powershell
$api = Get-FileHash 'Output\LightInDark.dll' -Algorithm SHA256
$mod = Get-FileHash 'Output\Light.dll' -Algorithm SHA256
# 取前 4 字节 → int32(小端)
[BitConverter]::ToInt32([Convert]::FromHexString($api.Hash)[0..3], 0)
[BitConverter]::ToInt32([Convert]::FromHexString($mod.Hash)[0..3], 0)
```
建议做成 `build.ps1`:构建 → 算 hash → 提示填到 `official.json`。**每次发版手动时容易忘,务必自动化。**

---

## 三、模组侧配置(玩家的 Settings.json)

`LightUserDataPath/Settings.json`(与 MaxFPS 同文件):

```json
{
  "VerifyServerUrl": "https://lidverify.moonscar.cn",
  "EnableHandshake": false,
  "HandshakeMode": 0,
  "HandshakeTimeoutSeconds": 10
}
```

| 字段 | 值 | 效果 |
|---|---|---|
| `EnableHandshake` | `false`(默认) | 房主不强制验证；普通客户端仍响应可信房主挑战 |
| | `true` | 房主启用握手验证 |
| `VerifyServerUrl` | 空 | 房主不启用验证，普通客户端也不请求票据 |
| | `https://lidverify.moonscar.cn`(默认非空) | 启用时请求此服务器签发票据 |
| `HandshakeMode` | `0`(默认) | 仅提示(右下角原生提示) |
| | `1` | 提示后 3 秒踢出 |
| `HandshakeTimeoutSeconds` | `10`(默认,夹 1~60) | 加入后多久未完成握手即判定失败 |

房主须开启 `EnableHandshake`。普通客户端保持默认 `false` 也会响应房主；双方必须使用与内置公钥对应的签票服务，且服务器已登记实际发布 DLL 的 hash。新挑战使用新 nonce；丢包时每 2 秒重发同一挑战，当前票据仅用于同一挑战的重传。

---

## 四、不改代码的日常维护

| 场景 | 做法 |
|---|---|
| 发新版本模组 | 更新服务器 `official.json` 的 hash → 玩家自动通过 |
| 换服务器域名 | 改玩家 `Settings.json` 的 `VerifyServerUrl` + 重新嵌入公钥 |
| 被盗私钥 | **立刻**换密钥对:重新生成 → 更新服务器私钥 + 重新嵌入公钥 → 旧玩家票据全失效需重新获取 |
| 想强制所有人用 | `HandshakeMode=1` 只影响房主自己的判断;要全局强制需把默认值改代码或做云端配置下发 |

---

## 五、调试

- 模组日志 `LightLog.log`:`[Handshake]` 开头记录挑战/nonce/票据/验证结果
- 服务器日志:`/verify` 的 200/403
- 常见问题:
  - 大家都"版本不匹配" → 官方 hash 没同步 / 有人用了旧版本 dll
  - 一直"超时未握手" → 服务器地址没配 / 连不上服务器(票据拿不到)→ 检查 `VerifyServerUrl`
  - 公钥不对 → 验签失败 → 重新 `GET /pubkey` 嵌入