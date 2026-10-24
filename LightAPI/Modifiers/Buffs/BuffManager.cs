using System;
using System.Collections.Generic;
using System.Linq;
using LightInDark.Core;
using LightInDark.Game;
using LightInDark.RPCs;
using UnityEngine;

namespace LightInDark.Modifiers.Buffs
{
    /// <summary>
    /// Buff 管理器：按玩家维护限时效果，统一计时并聚合属性倍率。
    /// 房主用 <see cref="AddAndBroadcast"/> 下发，其余客户端由 RPC 落地。
    /// </summary>
    public static class BuffManager
    {
        private static readonly Dictionary<byte, List<RuntimeBuff>> _byPlayer = new();
        private static readonly Dictionary<byte, float> _baseViewDistance = new();
        private static readonly Dictionary<byte, Vector3> _baseScale = new();
        private static readonly Dictionary<byte, float> _baseSpeed = new();

        /// <summary>本地添加Buff（同 Key 刷新时长）。</summary>
        public static RuntimeBuff Add(Player player, BuffDefinition definition, float? duration = null)
        {
            try
            {
                if (player == null || player.Control == null || definition == null) return null;

                // 参数化定义（SpeedBuff 等）首次使用时登记，供 RPC 按 key 反查
                if (BuffRegistry.Get(definition.Key) == null) BuffRegistry.Register(definition);

                byte id = player.Control.PlayerId;
                if (!_byPlayer.TryGetValue(id, out var list))
                {
                    list = new List<RuntimeBuff>();
                    _byPlayer[id] = list;
                }

                var existing = list.FirstOrDefault(b => b.Definition.Key == definition.Key);
                if (existing != null)
                {
                    existing.Duration = duration ?? definition.DefaultDuration;
                    existing.Remaining = existing.Duration;
                    LightLogger.Log($"[Buff] {player.Name} 刷新 {definition.Key} → {existing.Duration:0.#}s");
                    return existing;
                }

                var buff = definition.CreateInstance(player);
                if (buff == null) return null;

                buff.Duration = duration ?? definition.DefaultDuration;
                buff.Remaining = buff.Duration;
                list.Add(buff);
                buff.Apply();

                LightLogger.Log($"[Buff] {player.Name} +{definition.Key}（{(buff.IsPermanent ? "整局" : $"{buff.Duration:0.#}s")}）");
                return buff;
            }
            catch (Exception ex)
            {
                LightLogger.LogError("BuffManager.Add", ex);
                return null;
            }
        }

        /// <summary>房主调用：本地生效并广播到其他客户端。</summary>
        public static RuntimeBuff AddAndBroadcast(Player player, BuffDefinition definition, float? duration = null)
        {
            var buff = Add(player, definition, duration);
            if (buff == null) return null;

            try
            {
                if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    RpcDefinitions.SyncBuff(player.Control.PlayerId, definition.Key, buff.Duration);
            }
            catch (Exception ex)
            {
                LightLogger.LogWarning($"[Buff] 广播 {definition.Key} 失败: {ex.Message}");
            }

            return buff;
        }

        public static bool Remove(Player player, string key)
        {
            try
            {
                if (player == null || player.Control == null || string.IsNullOrEmpty(key)) return false;
                if (!_byPlayer.TryGetValue(player.Control.PlayerId, out var list)) return false;

                var target = list.FirstOrDefault(b => b.Definition.Key == key);
                if (target == null) return false;

                list.Remove(target);
                target.Expire();
                LightLogger.Log($"[Buff] {player.Name} -{key}");
                return true;
            }
            catch (Exception ex)
            {
                LightLogger.LogError("BuffManager.Remove", ex);
                return false;
            }
        }

        public static void RemoveAll(Player player)
        {
            try
            {
                if (player == null || player.Control == null) return;
                if (!_byPlayer.TryGetValue(player.Control.PlayerId, out var list)) return;

                foreach (var buff in list) buff.Expire();
                list.Clear();
                _byPlayer.Remove(player.Control.PlayerId);
            }
            catch (Exception ex)
            {
                LightLogger.LogError("BuffManager.RemoveAll", ex);
            }
        }

        public static IReadOnlyList<RuntimeBuff> Get(Player player)
        {
            if (player == null || player.Control == null) return Array.Empty<RuntimeBuff>();
            return _byPlayer.TryGetValue(player.Control.PlayerId, out var list)
                ? (IReadOnlyList<RuntimeBuff>)list
                : Array.Empty<RuntimeBuff>();
        }

        public static bool Has(Player player, string key)
            => !string.IsNullOrEmpty(key) && Get(player).Any(b => b.Definition.Key == key);

        /// <summary>每帧计时：到期或控件失效的 Buff 自动移除。</summary>
        public static void Tick(float dt)
        {
            if (_byPlayer.Count == 0) return;

            try
            {
                foreach (var kv in _byPlayer.ToList())
                {
                    var list = kv.Value;
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        var buff = list[i];
                        if (buff.IsDeadObject)
                        {
                            list.RemoveAt(i);
                            continue;
                        }

                        buff.Tick(dt);
                        if (!buff.IsExpired) continue;

                        list.RemoveAt(i);
                        buff.Expire();
                    }

                    if (list.Count == 0) _byPlayer.Remove(kv.Key);
                }
            }
            catch (Exception ex)
            {
                LightLogger.LogWarning($"[BuffManager.Tick] {ex.Message}");
            }
        }

        /// <summary>聚合视野 / 体型倍率（速度由 <see cref="ApplySpeed"/> 在物理更新后写）。</summary>
        public static void ApplyAttributes()
        {
            if (_byPlayer.Count == 0 && _baseViewDistance.Count == 0 && _baseScale.Count == 0) return;

            try
            {
                foreach (var player in Player.AllPlayers)
                {
                    var pc = player?.Control;
                    if (pc == null) continue;

                    float viewMul = 1f, scaleMul = 1f;
                    foreach (var buff in Get(player))
                    {
                        if (buff.Definition is ViewDistanceBuff view) viewMul *= view.Multiplier;
                        else if (buff.Definition is ScaleBuff scale) scaleMul *= scale.Factor;
                    }

                    // 视野只影响本人（别人的灯光半径与自己无关）
                    if (pc.AmOwner) ApplyViewDistance(pc, viewMul);
                    ApplyScale(pc, scaleMul);
                }
            }
            catch (Exception ex)
            {
                LightLogger.LogWarning($"[BuffManager.ApplyAttributes] {ex.Message}");
            }
        }

        /// <summary>速度倍率（由 <c>PlayerPhysics.FixedUpdate</c> 后置调用）。</summary>
        public static void ApplySpeed(PlayerControl control)
        {
            try
            {
                if (control == null || control.MyPhysics == null) return;

                var player = Player.Get(control);
                if (player == null) return;

                float mul = 1f;
                foreach (var buff in Get(player))
                    if (buff.Definition is SpeedBuff speed) mul *= speed.Multiplier;

                byte id = control.PlayerId;

                if (Mathf.Approximately(mul, 1f))
                {
                    if (_baseSpeed.TryGetValue(id, out float baseValue))
                    {
                        control.MyPhysics.Speed = baseValue;
                        _baseSpeed.Remove(id);
                    }
                    return;
                }

                // 首次生效时记录原始速度，之后按基线乘倍率，避免每帧在已放大的值上反复相乘
                if (!_baseSpeed.ContainsKey(id))
                    _baseSpeed[id] = control.MyPhysics.TrueSpeed;

                control.MyPhysics.Speed = _baseSpeed[id] * mul;
            }
            catch (Exception ex)
            {
                LightLogger.LogWarning($"[BuffManager.ApplySpeed] {ex.Message}");
            }
        }

        /// <summary>会议开始：结束所有"不跨会议"的 Buff。</summary>
        public static void OnMeetingStart()
        {
            if (_byPlayer.Count == 0) return;

            try
            {
                foreach (var kv in _byPlayer.ToList())
                {
                    var list = kv.Value;
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        var buff = list[i];
                        if (buff.Definition.CanPassMeeting) continue;

                        list.RemoveAt(i);
                        buff.Expire();
                    }

                    if (list.Count == 0) _byPlayer.Remove(kv.Key);
                }
            }
            catch (Exception ex)
            {
                LightLogger.LogError("BuffManager.OnMeetingStart", ex);
            }
        }

        /// <summary>换局 / 离开：清空全部 Buff 并恢复被改写的属性。</summary>
        public static void ClearAll()
        {
            try
            {
                foreach (var list in _byPlayer.Values)
                    foreach (var buff in list)
                        try { buff.Expire(); } catch { }

                _byPlayer.Clear();
                RestoreAttributes();
            }
            catch (Exception ex)
            {
                LightLogger.LogError("BuffManager.ClearAll", ex);
            }
        }

        // ---- 属性写入 ----

        private static void ApplyViewDistance(PlayerControl control, float mul)
        {
            try
            {
                if (control.lightSource == null) return;
                byte id = control.PlayerId;

                if (Mathf.Approximately(mul, 1f))
                {
                    if (_baseViewDistance.TryGetValue(id, out float baseValue))
                    {
                        control.lightSource.SetViewDistance(baseValue);
                        _baseViewDistance.Remove(id);
                    }
                    return;
                }

                if (!_baseViewDistance.ContainsKey(id))
                    _baseViewDistance[id] = control.lightSource.ViewDistance;

                float target = _baseViewDistance[id] * mul;
                if (Mathf.Abs(control.lightSource.ViewDistance - target) > 0.01f)
                    control.lightSource.SetViewDistance(target);
            }
            catch { }
        }

        private static void ApplyScale(PlayerControl control, float mul)
        {
            try
            {
                byte id = control.PlayerId;
                var transform = control.transform;

                if (Mathf.Approximately(mul, 1f))
                {
                    if (_baseScale.TryGetValue(id, out var baseValue))
                    {
                        transform.localScale = baseValue;
                        _baseScale.Remove(id);
                    }
                    return;
                }

                if (!_baseScale.ContainsKey(id))
                    _baseScale[id] = transform.localScale;

                Vector3 target = _baseScale[id] * mul;
                if ((transform.localScale - target).sqrMagnitude > 0.0001f)
                    transform.localScale = target;
            }
            catch { }
        }

        private static void RestoreAttributes()
        {
            foreach (var kv in _baseViewDistance.ToList())
            {
                try
                {
                    var control = Player.Get(kv.Key)?.Control;
                    if (control != null && control.lightSource != null)
                        control.lightSource.SetViewDistance(kv.Value);
                }
                catch { }
            }
            _baseViewDistance.Clear();

            foreach (var kv in _baseScale.ToList())
            {
                try
                {
                    var control = Player.Get(kv.Key)?.Control;
                    if (control != null) control.transform.localScale = kv.Value;
                }
                catch { }
            }
            _baseScale.Clear();

            foreach (var kv in _baseSpeed.ToList())
            {
                try
                {
                    var control = Player.Get(kv.Key)?.Control;
                    if (control != null && control.MyPhysics != null)
                        control.MyPhysics.Speed = kv.Value;
                }
                catch { }
            }
            _baseSpeed.Clear();
        }
    }
}
