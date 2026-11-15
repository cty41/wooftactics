# Godot Incident Schema

每条 Incident 的 YAML frontmatter 必须包含：

```yaml
id: lower-kebab-case
status: observed | reproduced | verified | superseded
signature: normalized first error signature
godot_version: exact version or unknown
dotnet_sdk: exact version or unknown
os: exact OS family/build when known
context: editor | runtime | headless | export | build
language: csharp | gdscript | gdextension | mixed
last_verified: YYYY-MM-DD
```

正文必须包含 `Observed`、`Reproduction`、`Cause and resolution`、`Evidence`、`Scope and invalidation`。证据标记使用 Research Guide 等级。

晋升顺序：首次出现 `observed`；稳定复现 `reproduced`；修复并有测试/人工证据 `verified`；版本或实现替代后 `superseded`。只有 verified 摘要进入 OKF，只有重复使用的流程才更新 Skill。

新事故在 `Cause and resolution` 里写明本该由哪一层拦住。允许的层是：本地测试、统一验证器、评审契约、人工验收，或当时没有这层。这不是新的 frontmatter 字段，也不是新的必填二级标题。不回填已有事故。
