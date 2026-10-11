using System;
using System.Collections.Generic;
using Hazel;
using LightInDark.Core;
using UnityEngine;

namespace LightInDark.RPCs;

// ═══════════════════════════════════════════════════════════════════════════
//  **泛型 RemoteProcess** —— 2026-10-11
//
//  用户："顺便把那个泛型 remote 搬过来，我之前写 nos addon 觉得这个挺有用"
//
//  来历：NOS(Nebula)的 `RemoteProcess<Parameter>` / `DivisibleRemoteProcess<,>`
//        （`NebulaPluginNova\Modules\NebulaRPC.cs:604 / 748`）✓
//        —— 写 addon 时**不用为每种参数手写一个 RPC 方法** ✓，
//           声明一个 `RemoteProcess<(int, string)>` 就能收发 ✓。
//
//  实现：建在我们自己的 `CustomRPC` 之上 ✓
//        （`CustomRPC.Register(hash, reader)` / `CustomRPC.Send(hash, writer)` ✓）
//        —— 所以它跟着我们的 RPC 系统走 ✓，不依赖 NOS ✓。
//
//  ⚠️ 用法（addon 里）：
//  <code>
//      // ① 声明一次（字段/静态都行 ✓；名字全局唯一 ✓）
//      static readonly RemoteProcess<(int round, string reason)> RpcRoundEnd
//          = new("MyAddon.RoundEnd", (arg, sender) => {
//                LightLogger.Log($"收到：第 {arg.round} 局结束，原因 {arg.reason}（来自 {sender}）✓");
//            });
//
//      // ② 任意地方发（会发到所有客户端，并在本地立即执行一次 ✓）
//      RpcRoundEnd.Invoke((3, "tasks"));
//  </code>
//
//  ⚠️ 支持的类型：基础类型 / string / Vector2 / Vector3 / 枚举 / 元组(2~4 个) / 以上类型的数组 ✓
//     自定义类型用 `RpcSerializer.Register<T>(写, 读)` 注册一次即可 ✓
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// 泛型参数序列化（`RemoteProcess` 用）✓
/// 基础类型写死支持 ✓；自定义类型用 <see cref="Register{T}"/> 挂一对读写函数 ✓。
/// </summary>
public static class RpcSerializer
{
    private static readonly Dictionary<Type, Action<MessageWriter, object>> _writers = new();
    private static readonly Dictionary<Type, Func<MessageReader, object>> _readers = new();

    /// <summary>注册一个自定义类型的读写 ✓（同类型重复注册 = 后者覆盖 ✓）</summary>
    public static void Register<T>(Action<MessageWriter, T> write, Func<MessageReader, T> read)
    {
        var t = typeof(T);
        _writers[t] = (w, v) => write(w, (T)v);
        _readers[t] = r => read(r)!;
    }

    public static bool CanHandle(Type t) => _writers.ContainsKey(t) || IsBuiltin(t) || IsTuple(t) || t.IsArray;

    private static bool IsBuiltin(Type t) =>
        t == typeof(bool) || t == typeof(byte) || t == typeof(sbyte) ||
        t == typeof(short) || t == typeof(ushort) || t == typeof(int) || t == typeof(uint) ||
        t == typeof(float) || t == typeof(string) || t == typeof(Vector2) || t == typeof(Vector3) ||
        t.IsEnum;

    private static bool IsTuple(Type t) =>
        t.IsGenericType && t.FullName != null && t.FullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal);

    // ── 写 ──────────────────────────────────────────────────────────────
    public static void Write<T>(MessageWriter w, T value)
    {
        var t = typeof(T);

        if (_writers.TryGetValue(t, out var cw)) { cw(w, value!); return; }

        if (t.IsEnum) { w.Write(Convert.ToInt32(value)); return; }

        if (t == typeof(bool)) { w.Write((bool)(object)value!); return; }
        if (t == typeof(byte)) { w.Write((byte)(object)value!); return; }
        if (t == typeof(sbyte)) { w.Write((sbyte)(object)value!); return; }
        if (t == typeof(short)) { w.Write((short)(object)value!); return; }
        if (t == typeof(ushort)) { w.Write((ushort)(object)value!); return; }
        if (t == typeof(int)) { w.Write((int)(object)value!); return; }
        if (t == typeof(uint)) { w.Write((uint)(object)value!); return; }
        if (t == typeof(float)) { w.Write((float)(object)value!); return; }
        if (t == typeof(string)) { w.Write((string)(object)value! ?? ""); return; }
        if (t == typeof(Vector2)) { var v = (Vector2)(object)value!; w.Write(v.x); w.Write(v.y); return; }
        if (t == typeof(Vector3)) { var v = (Vector3)(object)value!; w.Write(v.x); w.Write(v.y); w.Write(v.z); return; }

        if (t.IsArray)
        {
            var arr = (Array)(object)value!;
            w.Write(arr.Length);
            var elem = t.GetElementType()!;
            foreach (var item in arr) WriteBoxed(w, elem, item!);
            return;
        }

        var args = t.GetGenericArguments();
        if (IsTuple(t) && args.Length is >= 2 and <= 4)
        {
            WriteBoxed(w, args[0], GetField(value!, "Item1"));
            WriteBoxed(w, args[1], GetField(value!, "Item2"));
            if (args.Length >= 3) WriteBoxed(w, args[2], GetField(value!, "Item3"));
            if (args.Length >= 4) WriteBoxed(w, args[3], GetField(value!, "Item4"));
            return;
        }

        throw new NotSupportedException(
            $"[RemoteProcess] 类型 {t.FullName} 不能自动序列化 ✗ —— 请用 RpcSerializer.Register<{t.Name}>(写, 读) 注册一次 ✓");
    }

    // ── 读 ──────────────────────────────────────────────────────────────
    public static T Read<T>(MessageReader r)
    {
        var t = typeof(T);

        if (_readers.TryGetValue(t, out var cr)) return (T)cr(r);

        if (t.IsEnum) return (T)Enum.ToObject(t, r.ReadInt32());

        if (t == typeof(bool)) return (T)(object)r.ReadBoolean();
        if (t == typeof(byte)) return (T)(object)r.ReadByte();
        if (t == typeof(sbyte)) return (T)(object)r.ReadSByte();
        if (t == typeof(short)) return (T)(object)r.ReadInt16();
        if (t == typeof(ushort)) return (T)(object)r.ReadUInt16();
        if (t == typeof(int)) return (T)(object)r.ReadInt32();
        if (t == typeof(uint)) return (T)(object)r.ReadUInt32();
        if (t == typeof(float)) return (T)(object)r.ReadSingle();
        if (t == typeof(string)) return (T)(object)r.ReadString();
        if (t == typeof(Vector2)) return (T)(object)new Vector2(r.ReadSingle(), r.ReadSingle());
        if (t == typeof(Vector3)) return (T)(object)new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        if (t.IsArray)
        {
            int len = r.ReadInt32();
            var elem = t.GetElementType()!;
            var arr = Array.CreateInstance(elem, len);
            for (int i = 0; i < len; i++) arr.SetValue(ReadBoxed(r, elem), i);
            return (T)(object)arr;
        }

        var args = t.GetGenericArguments();
        if (IsTuple(t) && args.Length is >= 2 and <= 4)
        {
            var vals = new object[args.Length];
            for (int i = 0; i < args.Length; i++) vals[i] = ReadBoxed(r, args[i]);
            return (T)Activator.CreateInstance(t, vals)!;
        }

        throw new NotSupportedException(
            $"[RemoteProcess] 类型 {t.FullName} 不能自动反序列化 ✗ —— 请用 RpcSerializer.Register<{t.Name}>(写, 读) 注册一次 ✓");
    }

    // 装箱版本（数组 / 元组内部用）✓
    private static void WriteBoxed(MessageWriter w, Type t, object v)
    {
        var m = typeof(RpcSerializer).GetMethod(nameof(Write), new[] { typeof(MessageWriter), t });
        if (m != null) { m.Invoke(null, new[] { w, v }); return; }
        // 泛型推断拿不到时退到反射调用 Write<T>
        typeof(RpcSerializer).GetMethod(nameof(Write))!.MakeGenericMethod(t).Invoke(null, new[] { w, v });
    }

    private static object ReadBoxed(MessageReader r, Type t)
        => typeof(RpcSerializer).GetMethod(nameof(Read))!.MakeGenericMethod(t).Invoke(null, new object[] { r })!;

    private static object GetField(object tuple, string name)
        => tuple.GetType().GetField(name)!.GetValue(tuple)!;
}

/// <summary>
/// 一个"带参数的 RPC" ✓ —— 声明一次，到处 `Invoke` ✓（NOS 的 `RemoteProcess&lt;T&gt;` 等价物 ✓）
/// </summary>
public class RemoteProcess<T>
{
    /// <summary>RPC 名字（全局唯一 ✓）</summary>
    public string Name { get; }

    private readonly Action<T, PlayerControl?> _onReceive;

    /// <param name="name">RPC 名（建议带自己的前缀，如 "MyAddon.DoSomething" ✓）</param>
    /// <param name="onReceive">收到时执行（第 2 个参数 = 发送者；本地立即执行时传 LocalPlayer ✓）</param>
    public RemoteProcess(string name, Action<T, PlayerControl?> onReceive)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _onReceive = onReceive ?? throw new ArgumentNullException(nameof(onReceive));

        CustomRPC.Register(name, reader =>
        {
            try
            {
                var arg = RpcSerializer.Read<T>(reader);
                _onReceive(arg, ResolveSender(CustomRPC.CurrentSender));
            }
            catch (Exception ex)
            {
                LightLogger.LogError($"[RemoteProcess:{name}] 接收失败", ex);
            }
        });

        LightLogger.Log($"[RemoteProcess] 已注册 {name} （参数 {typeof(T).Name} ✓）");
    }

    /// <summary>发送到所有客户端，并在本地立即执行一次 ✓</summary>
    public void Invoke(T arg, bool reliable = true)
    {
        try { CustomRPC.Send(Name, w => RpcSerializer.Write(w, arg), reliable); }
        catch (Exception ex) { LightLogger.LogError($"[RemoteProcess:{Name}] 发送失败", ex); }
    }

    /// <summary>只发给某个人 ✓</summary>
    public void InvokeTo(PlayerControl target, T arg, bool reliable = true)
    {
        if (target == null) return;
        try { CustomRPC.SendTo(target, Name, w => RpcSerializer.Write(w, arg), reliable); }
        catch (Exception ex) { LightLogger.LogError($"[RemoteProcess:{Name}] 定向发送失败", ex); }
    }

    private static PlayerControl? ResolveSender(byte playerId)
    {
        try
        {
            foreach (var pc in PlayerControl.AllPlayerControls)
                if (pc != null && pc.PlayerId == playerId) return pc;
        }
        catch { }
        return null;
    }
}

/// <summary>不带参数的 `RemoteProcess` ✓</summary>
public class RemoteProcess
{
    public string Name { get; }
    private readonly Action<PlayerControl?> _onReceive;

    public RemoteProcess(string name, Action<PlayerControl?> onReceive)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _onReceive = onReceive ?? throw new ArgumentNullException(nameof(onReceive));

        CustomRPC.Register(name, reader =>
        {
            try { _onReceive(PlayerControl.LocalPlayer); }
            catch (Exception ex) { LightLogger.LogError($"[RemoteProcess:{name}] 接收失败", ex); }
        });
    }

    public void Invoke(bool reliable = true)
    {
        try { CustomRPC.Send(Name, _ => { }, reliable); }
        catch (Exception ex) { LightLogger.LogError($"[RemoteProcess:{Name}] 发送失败", ex); }
    }
}

/// <summary>
/// NOS 的 `DivisibleRemoteProcess&lt;T, TDivided&gt;` 等价物 ✓
/// —— 每帧把**多次调用**攒起来，之后一次性发一条（参数列表合并）✓
/// 适合"高频率、可合并"的通知（如每帧的坐标同步 ✓）。
/// </summary>
public class DivisibleRemoteProcess<T, TDivided>
{
    public string Name { get; }
    private readonly Func<T, TDivided> _divide;
    private readonly Action<T, TDivided, PlayerControl?> _onReceive;
    private readonly List<T> _pending = new();
    private readonly List<TDivided> _pendingDivided = new();

    public DivisibleRemoteProcess(string name, Func<T, TDivided> divide, Action<T, TDivided, PlayerControl?> onReceive)
    {
        Name = name; _divide = divide; _onReceive = onReceive;

        CustomRPC.Register(name, reader =>
        {
            try
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var arg = RpcSerializer.Read<T>(reader);
                    var divided = RpcSerializer.Read<TDivided>(reader);
                    _onReceive(arg, divided, PlayerControl.LocalPlayer);
                }
            }
            catch (Exception ex) { LightLogger.LogError($"[DivisibleRemoteProcess:{name}] 接收失败", ex); }
        });
    }

    /// <summary>攒一次（不会立即发送 ✓ 由 <see cref="Flush"/> 合并发出 ✓）</summary>
    public void Invoke(T arg)
    {
        _pending.Add(arg);
        _pendingDivided.Add(_divide(arg));
    }

    /// <summary>把所有攒下的合并成一条发出去 ✓</summary>
    public void Flush(bool reliable = true)
    {
        if (_pending.Count == 0) return;
        try
        {
            var args = _pending.ToArray();
            var divided = _pendingDivided.ToArray();
            CustomRPC.Send(Name, w =>
            {
                w.Write(args.Length);
                for (int i = 0; i < args.Length; i++)
                {
                    RpcSerializer.Write<T>(w, args[i]);
                    RpcSerializer.Write<TDivided>(w, divided[i]);
                }
            }, reliable);
        }
        catch (Exception ex) { LightLogger.LogError($"[DivisibleRemoteProcess:{Name}] 发送失败", ex); }
        finally { _pending.Clear(); _pendingDivided.Clear(); }
    }
}
