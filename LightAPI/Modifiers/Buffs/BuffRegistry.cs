using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LightInDark.Core;

namespace LightInDark.Modifiers.Buffs
{
    /// <summary>
    /// Buff 注册表：按 <see cref="BuffDefinition.Key"/> 索引，供 RPC 同步时按 key 反查定义。
    /// </summary>
    public static class BuffRegistry
    {
        private static readonly Dictionary<string, BuffDefinition> _byKey = new();

        public static IReadOnlyCollection<BuffDefinition> All => _byKey.Values;

        public static void Register(BuffDefinition buff)
        {
            if (buff == null || string.IsNullOrEmpty(buff.Key)) return;
            _byKey[buff.Key] = buff;
        }

        public static BuffDefinition Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return _byKey.TryGetValue(key, out var buff) ? buff : null;
        }

        /// <summary>扫描程序集里的 Buff 定义（优先取类里的静态单例，与职业注册同一套路）。</summary>
        public static void RegisterAssembly(Assembly assembly)
        {
            try
            {
                if (assembly == null) return;
                int count = 0;

                foreach (var type in SafeGetTypes(assembly))
                {
                    if (!typeof(BuffDefinition).IsAssignableFrom(type)) continue;
                    if (type.IsAbstract || !type.IsClass) continue;
                    if (_byKey.Values.Any(b => b.GetType() == type)) continue;

                    try
                    {
                        var singleton = type
                            .GetFields(BindingFlags.Public | BindingFlags.Static)
                            .FirstOrDefault(f => typeof(BuffDefinition).IsAssignableFrom(f.FieldType))
                            ?.GetValue(null) as BuffDefinition;

                        // 只注册带静态单例的定义；参数化定义（SpeedBuff 等）由 Add 时懒注册
                        if (singleton == null) continue;

                        Register(singleton);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        LightLogger.LogWarning($"[BuffRegistry] 注册 {type.Name} 失败: {ex.Message}");
                    }
                }

                LightLogger.Log($"[BuffRegistry] 注册 Buff {count} 个（累计 {_byKey.Count}）");
            }
            catch (Exception ex)
            {
                LightLogger.LogError("BuffRegistry.RegisterAssembly", ex);
            }
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }
    }
}
