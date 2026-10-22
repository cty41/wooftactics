---
id: gdunit-native-illegal-instruction
status: observed
signature: "GodotRuntimeTestRunner ends with exit code: -1073741795"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.316
os: Windows
context: runtime
language: csharp
last_verified: 2026-09-22
---

# GdUnit原生runner异常退出后外层等待

## Observed

Act改造验证中多次观察到runner返回 `-1073741795`，但外层 `dotnet test` 未自动结算，需要取消该测试job。这不是已定位的C#业务缺陷或已修复的引擎问题。

第二次发生于先隔离运行ResidentBattleBoard套件、再启动独立PlayableRunUi套件。UI的 `AbandonDuringActLoadingRejectsLatePreparedAndStaleEncounter` 先报告 `Non-static field requires a target`（反射读取Prepared时loader为空），随后runner退出。两者是否同根因未知。给该测试增加页面/status/commit诊断后，单例及完整UI47项均通过，第6轮该两次复验未再出现空loader。

第7轮新增诊断再次捕捉同一UI用例失败：`page=PREPARING ACT; status=Act preparation failed: act.catalog_missing; commit=True`，之后仍为上述原生退出码。因此空loader这次来自Act目录加载失败，而非已完成准备。Act入口现改用主目录已采用的`RequiredResourceLoader`（CacheMode.Ignore及精确路径/类型诊断）；缓存替身回归和两次完整UI均通过。该受测边界改进不能证明原生退出的引擎根因，状态仍observed。

## Reproduction

- 串行build `godot/tests/Tactics.Godot.TestHost.csproj`。
- 独立 `dotnet test ... --no-build --filter FullyQualifiedName~ResidentBattleBoardGodotTests`，再独立运行 `PlayableRunUiGodotTests`。
- 该序列并非稳定复现；不要把一次成功重跑当作修复。

## Cause and resolution

根因未确认。保留首条托管错误和原生退出码；原生runner已退出时，只取消当前外层测试job，禁止清缓存、杀其他Editor/attach或把退出码包装成通过。进一步需要崩溃栈或可重复的最小样本。

## Evidence

- `observed`: 同会话job pwsh-19输出：新借用棋盘3项通过，UI反射错误后runner退出 `-1073741795`，job由父取消并收集。
- `observed`: 更早Act基础组合验证也出现该原生退出码，未找到可靠根因。
- `verified_local`: 后续借用棋盘4项、失败UI单例、独立完整UI47项通过；仅证明这些运行成功。
- GdUnit adapter 3.1.1.0 / engine 5.1.0.0；runtime日志报告OpenGL Compatibility、Intel Arc B370。此信息不是驱动故障结论。

## Scope and invalidation

当前仅记录本机Godot4.7.1 Mono/GdUnit观察。没有稳定复现和根因修复前保持observed；不晋升为通用Engine workaround。
