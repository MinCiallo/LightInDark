using System;
using System.Collections.Generic;

namespace LightInDark.Game;

/// <summary>
/// **`Player` 的别名入口**（2026-10-16 ✓）。
///
/// ⚠️ 由来：用户给外部代码写的是 **`LightPlayer.TryGetPlayer(p =&gt; !p.IsDead &amp;&amp; …)`** ✓
///    （原话："比如 LightPlayer.TryGetPlayer(…)" ✓），而本工程这一层的类名是 **`Player`** ✓。
///    → 与其改名（`Player` 已经被大量现有代码引用 ✗，改名会牵动一大片 ✓），
///      不如**补一个静态别名** ✓：两种写法都能用 ✓，一个现有调用点都不用改 ✓。
///
/// ⚠️ 这里**只是转发** ✓ —— 真正的实现、注释、坑都在 <see cref="Player"/> 里 ✓
///    （要改行为请改那边 ✓，别在这里加逻辑 ✗）
/// </summary>
public static class LightPlayer
{
    /// <summary>本地玩家 ✓（拿不到返回 null ✓）</summary>
    public static Player? LocalPlayer => Player.LocalPlayer;

    /// <summary>房主 ✓</summary>
    public static Player? HostPlayer => Player.HostPlayer;

    /// <summary>全部玩家 ✓</summary>
    public static IEnumerable<Player> AllPlayers => Player.AllPlayers;

    /// <summary>按 PlayerId 取 ✓</summary>
    public static Player? Get(byte playerId) => Player.Get(playerId);

    /// <summary>由原版 `PlayerControl` 反查我们的包装 ✓</summary>
    public static Player? Get(PlayerControl? control) => Player.Get(control);

    /// <summary>找第一个满足条件的玩家 ✓</summary>
    public static Player? Find(Func<Player, bool> predicate) => Player.Find(predicate);

    /// <summary>找全部满足条件的玩家 ✓</summary>
    public static List<Player> FindAll(Func<Player, bool> predicate) => Player.FindAll(predicate);

    /// <summary>尝试取一个玩家 ✓（用户点名的用法 ✓）</summary>
    public static bool TryGetPlayer(Func<Player, bool> predicate, out Player player)
        => Player.TryGetPlayer(predicate, out player);
}
