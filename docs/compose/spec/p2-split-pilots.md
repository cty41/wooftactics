---
feature: p2-split-pilots
status: delivered
updated: 2026-11-10
source: .agents/plans/2026-11-10-p2-split-pilots.md
---

# P2 拆分试点：OKF 同步链与门禁日志分诊

## Report

**What was built** — `.agents/rules/subagent-handback.md` 新增「已验证派发配方」两节：OKF 同步链（机械段四步 report → 汇总 → 逐 scope sync --write → validate_bundle，附四要素任务书模板）与门禁日志分诊（只读失败分诊、签名锚点输出、附任务书模板）。两个配方各经一次真实子代理试跑验收。`.agents/rules/knowledge-maintenance.md` 与 OKF 概念（`okf-maintenance`、`godot-agent-workflow`）补上机械段可拆、正文落笔归主循环的边界表述。

**Verification** —

OKF 同步链试跑（载体 = 本次 rules 修改触发的 `godot-agent-workflow` / `project-documentation` scope）：子代理执行机械段四步，回执四段齐备；`validate_bundle.py` 退出码 0，主循环独立重跑核对一致；`git diff` 确认主循环落笔的概念正文完好，sync 仅写 timestamp / source_fingerprint 与根日志 Sync 记录。试跑暴露任务书「不改 log.md」与 `sync --write` 固有行为的字面冲突，回执如实记入偏差点，配方措辞已改为「不手动编辑；sync --write 自动产出除外」。

门禁日志分诊试跑（载体 = 1026 行真实测试日志，捕获命令与时间见日志头）：5 个批次 779 个测试中 5 个真实失败（`test_windows_rc_pipeline`，根因是脚本内 `python` 命中 Windows Store 别名）。子代理逐 case 给出行号锚点与错误签名，批次计数与汇总行核对一致；主循环按行号抽查 4 处锚点（L896、L936、L365、L985）全部对得上。日志未合成，分诊结论未过滤环境噪音。

两份规则的「正文落笔不拆、机械段可拆」表述经交叉复核一致；`knowledge-maintenance.md` Source Change Sync 节补了指向 `subagent-handback.md` 的一行。纯规则/知识文档变更，无产品代码测试适用。

## Journey log

- 试跑暴露的边界经验：机械段的「机械」要按命令固有行为划界，不能按任务书字面禁令划界——`sync --write` 会自动写 metadata 与根日志，这类产出算机械段；真正的边界是「不许手写正文」。
- 环境噪音会被带进分诊结论（本次 5 个失败全是 `python` 别名问题，不是产品回归）。分诊回执如实呈现即可，不替主循环做「这是环境问题所以忽略」的裁决。
- 另有一个独立环境坑本次定位到根因：shell 会把 `ProgramFiles` 置空，NuGet 的 `CalculateFolderPath` 因此抛 `path1` 空引用，`dotnet restore/test` 全挂。置回 `C:\Program Files` 后恢复。与分诊试点的 5 个失败同属「环境假信号」家族。
