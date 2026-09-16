---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/main/Tools/okf
title: OKF Maintenance
description: 将工作区变更映射到 catalog_scope，并由 Agent 同步受影响知识概念的维护流程。
tags: [agent, okf, knowledge, automation]
timestamp: "2026-09-17T00:07:58+08:00"
status: active
catalog_scope: okf-maintenance
repo_paths:
  - .agents/knowledge/catalog-scopes.yaml
  - .agents/rules/knowledge-maintenance.md
  - .agents/skills/knowledge-maintenance/SKILL.md
  - Tools/okf/catalog_impact.py
  - Tools/okf/validate_bundle.py
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:6fe1ef2aac5e0f0dac5479e110f0a562dd23c81d115b17adb637f54a2e5fdaa8
---

# Current State

`catalog-scopes.yaml` 保存仓库路径到 `catalog_scope` 的多对多映射；根 `README.md` 作为项目入口归入 `project-architecture`。Agent 修改代码、设计、计划、规则或工具后，使用 `catalog_impact.py report --worktree` 找出受影响概念，核对真实差异并更新知识正文，再使用 `sync --worktree --scope <scope> --write` 刷新来源指纹、时间和根日志。

同一路径可以合法影响多个 scope，例如根 `AGENTS.md` 同时影响项目架构、Godot Agent 工作流和 OKF 维护约束。同步范围应以“本任务实际造成的路径变化”为准：共享路径产生的直接 scope 必须一并核对，工作区中由其他文件产生的无关 scope 继续排除。

`.agents/skills/project-doc-organization`、`plan-mode-plan-writer`、`make-dev-plan` 已上移全局 `cty41/skills`（`~/.agents/skills`），不再属于本仓库监控路径；本地只保留项目专属技能与 `knowledge-maintenance`（完整 `Tools/okf`）、`manual-qa-handoff`（被 `Tools/agent-policy` 硬引用）两个特化。

这一流程由 Agent 规则和本地轻量命令触发，不依赖 Git hook 或远端 CI，也不得仅为刷新 OKF/文档证据主动派发高耗时 GitHub workflow。未映射但位于受监控目录的路径会显示为警告，Agent 必须判断它应加入已有 scope、建立新概念，还是明确保持不受 OKF 管理。

当前设计来自 `.agents/docs/` 的主题权威文档，`brainstorm.md` 仅保存未经验证的临时灵感。当前任务只来自仍活跃的 `.agents/plans/`。计划完成后应先迁移长期知识并删除计划；OKF 中需要保留的历史概念使用 `archived` 或 `superseded`，不继续把旧计划当当前依据。

`validate_bundle.py --allow-missing-repo-prefix` 仅用于物理裁剪后的 ownership 验证副本：它按完整路径段边界允许指定前缀下的历史 `repo_paths` 缺失，其余 frontmatter、链接、scope、fingerprint 和路径仍严格校验。普通工作区不得传入该参数；常规 bundle 验证继续要求所有 `repo_paths` 实际存在。

Godot 引擎/工具链问题增加一层证据晋升：完整错误与复现先进入 `.agents/incidents/godot`，只有 `verified` 摘要进入 `godot-agent-workflow` OKF；研究方法和重复流程分别属于 Research Guide 与 Skill。`catalog-scopes.yaml` 已将 Incidents、Godot Skills、`src/`、`godot/` 和迁移验证工具纳入相应 scope。

# Relationships

- [Godot Agent Workflow](godot-agent-workflow.md)规定当前代码、Resource、文档和验证的通用安全边界；[Archived Unity Agent Workflow](unity-agent-workflow.md)仅用于历史追溯。
- [Project Documentation](project-documentation.md)规定 docs、活跃 plans、统一缺口和完成后清理的职责。
- [Godot Agent Workflow](godot-agent-workflow.md)规定 Godot Incident 到 OKF/Skill 的证据晋升边界。
- [Open Knowledge Format v0.1](../references/okf-v0.1.md)定义 bundle、概念、索引和日志的基础格式。

# Verification Guidance

运行影响检测、bundle 校验和 `Tools/okf` 下的全部单元测试。同步命令必须限定为本任务实际影响的 scope，避免吸收工作区中已有的无关修改。
