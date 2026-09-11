# 五红土松诗人 Attack 提示词

> **历史模板，不得直接用于新生成。** 以下六帧、臂腿及唐剑/古琴/酒具措辞与当前单帧、四爪直接附着、入鞘环首唐刀约束冲突。原文仅保留追溯；当前动作需求、来源职责与待确认项见[诗人动作覆盖矩阵](action_coverage_matrix.md)。矩阵本身不授权生图。

## 动作层模板

```text
Six-frame authored action using an independently approved prop; preserve upright capsule-dog anatomy, all four attached paws, breed identity, core body volume and fixed contact logic. Prop depth must be declared per pose.
```

## 完整组合模板

```text
[BASE STYLE] + [CHARACTER IDENTITY] + [APPROVED TANG SWORD OR GUQIN OR TRAVEL WINE VESSEL] + one declared isometric action, exact screen-space prop endpoints and near/far paw layering, no unapproved extra equipment.
```

## 六帧骨架

1. `frame 1 of 6` — head acquires target; torso coils; four legs brace; approved prop at anticipation anchor; identity fixed.
2. `frame 2 of 6` — head follows action axis; torso leans; near foreleg shifts; far foreleg supports; hindlegs brace; prop begins travel.
3. `frame 3 of 6` — head clear of prop; torso at release; forelegs preserve quadruped anatomy; hindlegs drive; prop reaches primary endpoint.
4. `frame 4 of 6` — head steady; torso at impact/focus; legs hold; prop at readable peak; no duplicate prop.
5. `frame 5 of 6` — head returns; torso recovers; forelegs return; hindlegs settle; prop retracts or remains according to action.
6. `frame 6 of 6` — head and torso approach idle; four paws restore baseline; prop returns to approved resting anchor; identity fixed.

具体侠客行、剑雨、琴奏与饮酒动作必须分别建立 composition，不能共用一套武器轨迹。
