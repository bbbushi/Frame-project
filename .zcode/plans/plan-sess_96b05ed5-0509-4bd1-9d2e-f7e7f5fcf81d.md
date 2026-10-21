### 连环处决（链式冲刺斩）实现计划

**流程**：子弹时间中左键标记敌人 → **E** → 恢复常速、暗幕退役 → 玩家进入连续冲刺：对准第一个标记敌人的方向突进，**穿过敌人即必杀**，不停留直接转向第二个继续冲……全程 Dash 动画姿态 + 每 0.03s 一道残影，链尾清标记、恢复重力、回正常状态。

### 核心机制（`PlayerBulletTime.ExecuteChain` 协程，替换瞬移版）

- **链开始**：`rb.gravityScale` 归零（存原值）→ `anim.CrossFade("Dash")` → `IsActive = false` → `Stage.RestoreAll()` 并销毁 Stage（暗幕/扇形退役、高亮对象恢复原层序）→ 恢复常速（0.3s）
- **每段冲刺**（对每个标记目标，依次）：
  1. 朝向目标方向（`SetFacing`）
  2. 沿方向以 `thrustForce.maxSpeed` **逐帧位移**（`transform.position += dir × speed × deltaTime`，冲刺穿墙风格）
  3. 与目标距离 < 0.8 时 `Hit(new Damage(9999f, Players, Melee, Light))` **必杀**（每段命中一次；走正常管线保留 Flash/击退/血迹）
  4. 冲过目标 0.8 单位（overshoot，穿越斩的惯性感）后本段结束
  5. 全程每 0.03s `ShadowPool.Instance.GetShadow` 生成残影
  6. 段间停顿 `chainDelay`（0.15s，`PlayerControllerData` 可配）
- **链尾**：gravityScale 恢复原值、`ClearAllMarks`、`_chainIndex = -1`（IsExecuting=false → ExecutionState 回 None）
- **防御**：目标回池/销毁跳过；玩家死亡（IsDead）立即中断并恢复重力
- **与旧蓝本的差异**：不再"瞬移到目标前 2f 再 ThrustCore"——改为协程连续位移直接穿过目标；不复用 `ThrustCore`（固定位移长度不适配可变目标距离），冲刺速度沿用 `thrustForce.maxSpeed` 配置

### 其余改动（输入/状态机接入，共 9 处小改 + 1 新文件）

1. `PlayerAction.cs`：+`Execution`（序号 6）
2. `PlayerInputData.cs`：默认表 + E/Down
3. `PlayerInputData.asset`：追加 `action: 6, key: 101`（E；SO 资产同步防死键）
4. 新建 `Commands/ExecutionCommand.cs`：CanExecute = 子弹时间激活 && MarkCount>0 && !IsExecuting；Execute = ChangeState(Execution)
5. `PlayerInputController.cs`：注册 + 轮询各一行
6. `PlayerStateMachine.cs`：ActionStateId +`Execution`
7. `ActionStates.cs`：+`PlayerExecutionState`（Enter 调 `BeginExecution()`；Update 查 `!IsExecuting` 回 None；animBoolName null——链中动画由协程驱动）
8. `PlayerAnimatorComponent.cs`：注册 Execution + 互斥组扩 `ForbidAllBetween(Attack, Thrust, Execution)`
9. `PlayerControllerData.cs`：+`chainDelay = 0.15f`
10. `PlayerBulletTime.cs`：+`_chainIndex/IsExecuting/_chainDelay`、`BeginExecution()`、`ExecuteChain()` 协程（上述核心）
11. `BulletTimeStage.cs`：+`RestoreAll()`（遍历 Drop 全部提升的 renderer 含玩家、清记录，供暗幕退役调用）

### 验证
编译零报错（MCP 复查）→ Play：右键进子弹时间 → 左键标 2~3 个分散的敌人 → E → 常速下连续冲刺穿过每个敌人（残影拖尾、逐个必杀、朝向自动对准下一个）→ 链尾回正常 → 边缘：单标记可 E；执行中左键/E 无效；敌人中途回池不炸；完全退出重进 Play 再测一轮