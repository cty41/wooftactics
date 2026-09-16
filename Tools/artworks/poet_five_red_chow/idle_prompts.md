# 五红土松诗人 Idle 提示词

## 动作层模板

```text
Idle action: calm dignified breathing in the upright capsule stance, two front paws attached directly to the body sides, two hind paws planted, head steady, ears alert, curled tail stable. Preserve the approved identity silhouette and fixed feet baseline.
```

## 完整组合模板

```text
[BASE STYLE] + [CHARACTER IDENTITY] + down-right 45-degree isometric upright capsule idle, near and far front paws attached directly to the capsule, two hind paws readable on the baseline, no equipment, no effects, fixed body center x=128 and feet baseline y=236.
```

## 六帧骨架

每帧均保持同一画布、体量、脚位和装备为空：

1. `frame 1 of 6` — head neutral; torso neutral; left foreleg planted; right foreleg planted; left hindleg planted; right hindleg planted; weapon absent; baseline fixed.
2. `frame 2 of 6` — head rises 1 px; torso expands subtly; all four legs planted; weapon absent; identity fixed.
3. `frame 3 of 6` — head steady; chest at inhale peak; all four legs unchanged; tail tip shifts minimally; weapon absent.
4. `frame 4 of 6` — head returns; torso neutral; all four legs planted; weapon absent; identity fixed.
5. `frame 5 of 6` — head lowers 1 px; torso compresses subtly; all four legs unchanged; weapon absent.
6. `frame 6 of 6` — head and torso approach frame 1; all four legs planted; tail returns; weapon absent; seamless loop.

## 可选 sequence sheet 测试

使用单张 `3×3` 等分 sheet：前 6 格按行读取六帧，第 7–9 格完全透明。所有格共享脚底基线和角色中心；切分后禁止逐帧 trim、重新居中、缩放或独立去幕。
