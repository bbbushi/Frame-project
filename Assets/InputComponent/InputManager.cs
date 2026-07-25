using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Frame.Player;
namespace Managers{

/// <summary>
/// 输入管理器 — 读取 PlayerInputData 绑定配置，每帧缓存输入状态。
/// 命令模式中的"硬件输入读取层"，通过 InputManager.IsPressed/IsHeld/IsReleased 查询。
///
/// 使用方式：GameManager.Get&lt;InputManager&gt;().IsPressed(PlayerAction.Jump)
///
/// 若未找到 PlayerInputData.asset，使用硬编码默认键位作为后备。
/// </summary>
public class InputManager : IManager, IUpdatable
{
    public string Name => "InputManager";
    public List<Type> Dependencies => new();

    // ── 配置 ──
    private PlayerInputData _inputData;

    // ── 每帧缓存 ──
    private readonly Dictionary<PlayerAction, bool> _pressed  = new();
    private readonly Dictionary<PlayerAction, bool> _held     = new();
    private readonly Dictionary<PlayerAction, bool> _released = new();

    // ── 移动方向（外部直接读取属性） ──
    public float MoveInput         { get; private set; }

    // ── 全局输入开关 ──
    public bool IsInputEnabled { get; set; } = true;

    // ═══════════════════════════════════════════════════════
    //  公开查询
    // ═══════════════════════════════════════════════════════
    public bool IsPressed(PlayerAction action)  => IsInputEnabled && GetValue(_pressed, action);
    public bool IsHeld(PlayerAction action)     => IsInputEnabled && GetValue(_held, action);
    public bool IsReleased(PlayerAction action) => IsInputEnabled && GetValue(_released, action);

    // ═══════════════════════════════════════════════════════
    //  IManager / IUpdatable
    // ═══════════════════════════════════════════════════════
    public IEnumerator Initialize()
    {
        _inputData = Resources.Load<PlayerInputData>("PlayerInputData");
        if (_inputData == null)
            Debug.LogWarning("[InputManager] 未在 Resources/ 中找到 PlayerInputData.asset，使用硬编码默认键位。");

        ResetFrameStates();
        MoveInput = 0f;
        yield break;
    }

    public void Deinitialize() { }

    public void OnUpdate(float deltaTime)
    {
        RefreshFrameState();
    }

    // ═══════════════════════════════════════════════════════
    //  内部
    // ═══════════════════════════════════════════════════════
    private void RefreshFrameState()
    {
        ResetFrameStates();

        // 读取硬件：优先使用 ScriptableObject，否则使用硬编码默认值
        if (_inputData != null && _inputData.bindings != null && _inputData.bindings.Count > 0)
        {
            foreach (var b in _inputData.bindings)
                ReadKey(b.action, b.key);
        }
        else
        {
            // ── 硬编码默认键位 ──
            ReadKey(PlayerAction.MoveLeft,  KeyCode.A);
            ReadKey(PlayerAction.MoveRight, KeyCode.D);
            ReadKey(PlayerAction.Jump,      KeyCode.Space);
            ReadKey(PlayerAction.Thrust,    KeyCode.S);
            ReadKey(PlayerAction.Attack,    KeyCode.Mouse0);
        }

        UpdateMoveInput();
    }

    private void ResetFrameStates()
    {
        foreach (PlayerAction a in Enum.GetValues(typeof(PlayerAction)))
        {
            _pressed[a] = false;
            _held[a] = false;
            _released[a] = false;
        }
    }

    private void ReadKey(PlayerAction action, KeyCode key)
    {
        if (Input.GetKeyDown(key)) _pressed[action] = true;
        if (Input.GetKey(key))     _held[action]    = true;
        if (Input.GetKeyUp(key))   _released[action] = true;
    }

    private void UpdateMoveInput()
    {
        float left  = IsHeld(PlayerAction.MoveLeft)  ? -1f : 0f;
        float right = IsHeld(PlayerAction.MoveRight) ?  1f : 0f;
        MoveInput = left + right;
    }

    private static bool GetValue(Dictionary<PlayerAction, bool> map, PlayerAction key)
        => map.TryGetValue(key, out bool v) && v;

    // ── 外部设置配置（如需运行时替换绑定） ──
    public void SetInputData(PlayerInputData data) => _inputData = data;
}
}