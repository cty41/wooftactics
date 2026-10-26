# Godot Incidents

按精确错误签名或上下文加载单条记录，不要默认扫描全部历史。

| Incident | 精确路由线索 | 状态 |
|---|---|---|
| [script-mainloop-entry](script-mainloop-entry.md) | `doesn't inherit from SceneTree or MainLoop` | verified |
| [csharp-assembly-reload-duplicate-type](csharp-assembly-reload-duplicate-type.md) | `same key has already been added` + `ScriptTypeBiMap.Add` | verified |
| [csharp-assembly-reload-field-type-mismatch](csharp-assembly-reload-field-type-mismatch.md) | `Unable to cast` + `RestoreGodotObjectData` after changing a live tool field type | verified |
| [editor-dock-lifecycle](editor-dock-lifecycle.md) | Dock 瞬间关闭、插件进入后退出 | verified |
| [typed-resource-reload](typed-resource-reload.md) | C# typed Resource 在 reload 后不可用 | verified |
| [export-release-editor-dependency-graph-contamination](export-release-editor-dependency-graph-contamination.md) | `EditorPlugin`/`EditorUndoRedoManager` missing + typed Resource 退化为 `Godot.Resource` | verified |
| [editor-resource-missing-tool](editor-resource-missing-tool.md) | `[GlobalClass]` Resource 在 EditorPlugin 中退化为 `Godot.Resource`，runtime/headless 正常 | verified |
| [wrong-project-root](wrong-project-root.md) | Build 成功但看不到 tooling/内容 | verified |
| [parallel-build-obj-contention](parallel-build-obj-contention.md) | Core/Godot 并行构建争抢 `obj`/DLL | verified |
| [gdu4-conditional-package-release-contamination](gdu4-conditional-package-release-contamination.md) | Release `.deps.json` 含 GdUnit/TestPlatform | verified |
| [gdunit-native-illegal-instruction](gdunit-native-illegal-instruction.md) | `GodotRuntimeTestRunner ends with exit code: -1073741795` | observed |
| [headless-resource-uid-cache](headless-resource-uid-cache.md) | `unregistered Resource UID` + standalone ResourceSaver process | verified |
| [godot-ai-project-run-cold-timeout](godot-ai-project-run-cold-timeout.md) | `Command run_project timed out after 5.0s` 但 Editor 已进入 live | observed |
| [gdunit-gameplay-journey-native-host-hang](gdunit-gameplay-journey-native-host-hang.md) | `GodotGameplayRuntimeRunnerTests` 长场景套件 Godot host 退出后 testhost 满载无输出挂起 | reproduced |

格式与晋升要求见 [schema](schema.md)。
