using System;
using System.Collections.Generic;
using LightInDark.Core;
using LightInDark.Game;
using UnityEngine;

namespace LightInDark.Visual
{
    /// <summary>
    /// 玩家外观覆盖：小黑人（匿名外观）。
    /// 效果：清空帽子 / 皮肤 / 面罩 / 宠物，身体改成灰色，名字隐藏；可恢复原样。
    /// </summary>
    public static class PlayerAppearance
    {
        private static readonly UnityEngine.Color Gray = new(0.6f, 0.6f, 0.6f, 1f);
        private static readonly Dictionary<byte, OriginalLook> _originals = new();
        private static readonly HashSet<byte> _anonymous = new();
        private static bool _globalAnonymous;

        private sealed class OriginalLook
        {
            public string HatId;
            public string SkinId;
            public string VisorId;
            public string PetId;
            public int ColorId;
        }

        /// <summary>全场小黑人是否开启。</summary>
        public static bool IsGlobalAnonymous => _globalAnonymous;

        public static bool IsAnonymous(Player player)
        {
            if (_globalAnonymous) return true;
            if (player == null || player.Control == null) return false;
            return _anonymous.Contains(player.Control.PlayerId);
        }

        /// <summary>全场生效：把当前所有玩家都变成小黑人（或全部恢复）。</summary>
        public static void SetAnonymousAll(bool enabled)
        {
            _globalAnonymous = enabled;
            foreach (var player in Player.AllPlayers)
            {
                try { SetAnonymous(player, enabled); }
                catch { }
            }
            LightLogger.Log($"[PlayerAppearance] 全场小黑人 {(enabled ? "开启" : "关闭")}");
        }

        /// <summary>开启 / 关闭匿名外观。</summary>
        public static void SetAnonymous(Player player, bool enabled)
        {
            try
            {
                if (player == null || player.Control == null) return;
                var control = player.Control;
                byte id = control.PlayerId;

                if (!enabled)
                {
                    _anonymous.Remove(id);
                    if (_originals.TryGetValue(id, out var old))
                    {
                        Restore(control, old);
                        _originals.Remove(id);
                    }
                    LightLogger.Log($"[PlayerAppearance] {player.Name} 恢复原样");
                    return;
                }

                if (!_originals.TryGetValue(id, out var look))
                {
                    look = Capture(control);
                    _originals[id] = look;
                }

                _anonymous.Add(id);
                Apply(control);
                LightLogger.Log($"[PlayerAppearance] {player.Name} 变为小黑人");
            }
            catch (Exception ex)
            {
                LightLogger.LogWarning($"[PlayerAppearance] SetAnonymous 失败: {ex.Message}");
            }
        }

        /// <summary>低频兜底：原版换装 / 场景重载后重新压回匿名外观。</summary>
        public static void EnsureApplied(Player player)
        {
            try
            {
                if (player == null || player.Control == null) return;
                if (!_globalAnonymous && !_anonymous.Contains(player.Control.PlayerId)) return;

                var control = player.Control;
                var cosmetics = control.cosmetics;
                if (cosmetics == null) return;

                bool nameHidden = cosmetics.nameTextContainer != null && !cosmetics.nameTextContainer.activeSelf;
                bool bald = cosmetics.hat == null || !cosmetics.hat.HasHat();
                if (nameHidden && bald) return;

                // 中途加入的玩家还没被记录过，先存一份原样，保证之后能恢复
                if (!_originals.ContainsKey(control.PlayerId)) _originals[control.PlayerId] = Capture(control);
                Apply(control);
            }
            catch (Exception ex)
            {
                LightLogger.LogWarning($"[PlayerAppearance] EnsureApplied 失败: {ex.Message}");
            }
        }

        /// <summary>换局 / 离开：恢复全部被改过的玩家。</summary>
        public static void ClearAll()
        {
            foreach (var kv in new List<KeyValuePair<byte, OriginalLook>>(_originals))
            {
                try
                {
                    var control = Player.Get(kv.Key)?.Control;
                    if (control != null) Restore(control, kv.Value);
                }
                catch { }
            }

            _originals.Clear();
            _anonymous.Clear();
            _globalAnonymous = false;
        }

        // ---- 内部实现 ----

        private static OriginalLook Capture(PlayerControl control)
        {
            var c = control.cosmetics;
            return new OriginalLook
            {
                HatId = c.hat != null && c.hat.Hat != null ? c.hat.Hat.ProductId : CosmeticsLayer.EMPTY_HAT_ID,
                SkinId = c.CurrentSkin != null && c.CurrentSkin.data != null ? c.CurrentSkin.data.ProductId : CosmeticsLayer.EMPTY_SKIN_ID,
                VisorId = c.CurrentVisor != null && c.CurrentVisor.visorData != null ? c.CurrentVisor.visorData.ProductId : CosmeticsLayer.EMPTY_VISOR_ID,
                PetId = c.CurrentPet != null && c.CurrentPet.data != null ? c.CurrentPet.data.ProductId : CosmeticsLayer.EMPTY_PET_ID,
                ColorId = c.ColorId,
            };
        }

        private static void Apply(PlayerControl control)
        {
            var c = control.cosmetics;
            if (c == null) return;

            // 清空装扮（与 NOS 的 UnknownOutfit 同款：无帽 / 无皮肤 / 无面罩 / 无宠物）
            control.SetHat(CosmeticsLayer.EMPTY_HAT_ID, c.ColorId);
            control.SetSkin(CosmeticsLayer.EMPTY_SKIN_ID, c.ColorId);
            control.SetVisor(CosmeticsLayer.EMPTY_VISOR_ID, c.ColorId);
            control.SetPet(CosmeticsLayer.EMPTY_PET_ID, c.ColorId);

            PaintGray(c);
            if (c.nameTextContainer != null) c.nameTextContainer.SetActive(false);
        }

        private static void Restore(PlayerControl control, OriginalLook look)
        {
            var c = control.cosmetics;
            if (c == null) return;

            control.SetHat(look.HatId ?? CosmeticsLayer.EMPTY_HAT_ID, look.ColorId);
            control.SetSkin(look.SkinId ?? CosmeticsLayer.EMPTY_SKIN_ID, look.ColorId);
            control.SetVisor(look.VisorId ?? CosmeticsLayer.EMPTY_VISOR_ID, look.ColorId);
            control.SetPet(look.PetId ?? CosmeticsLayer.EMPTY_PET_ID, look.ColorId);

            PaintBodyColor(c, look.ColorId);
            if (c.nameTextContainer != null) c.nameTextContainer.SetActive(true);
        }

        private static void PaintGray(CosmeticsLayer cosmetics)
        {
            try
            {
                ForEachBodyRenderer(cosmetics, renderer => PlayerMaterial.SetColors(Gray, renderer));
            }
            catch { }
        }

        private static void PaintBodyColor(CosmeticsLayer cosmetics, int colorId)
        {
            try
            {
                var color = (UnityEngine.Color)Palette.PlayerColors[colorId];
                ForEachBodyRenderer(cosmetics, renderer => PlayerMaterial.SetColors(color, renderer));
            }
            catch { }
        }

        private static void ForEachBodyRenderer(CosmeticsLayer cosmetics, Action<SpriteRenderer> action)
        {
            var bodies = cosmetics.bodySprites;
            if (bodies != null)
            {
                for (int i = 0; i < bodies.Count; i++)
                {
                    var body = bodies[i];
                    if (body == null) continue;

                    if (body.BodySprite != null) action(body.BodySprite);

                    var parts = body.LongModeParts;
                    if (parts == null) continue;
                    for (int j = 0; j < parts.Length; j++)
                        if (parts[j] != null) action(parts[j]);
                }
            }

            var skin = cosmetics.CurrentSkin;
            if (skin != null && skin.layer != null) action(skin.layer);
        }
    }
}
