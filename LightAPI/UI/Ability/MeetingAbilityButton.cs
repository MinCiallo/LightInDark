using System;
using LightInDark.Audio;
using LightInDark.Core;
using LightInDark.Game;
using LightInDark.Roles;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LightInDark.UI.Ability
{
    /// <summary>
    /// 会议右下角按钮：与普通技能按钮同款外观（克隆 HudManager.AbilityButton 模板），
    /// 但只在整个会议期间显示（会议开始创建、会议结束销毁）。
    /// 默认位置为会议界面右下角，可用 <see cref="MeetingAbilityButton"/> 的 Position 调整。
    ///
    /// 职业用法（OnActivated 中）：
    /// <code>
    /// MeetingAbilityButton.Create(role, new RoleButtonConfig()
    ///     .SetLabelKey("Button.Xxx.meeting")
    ///     .SetOnClickSFX("./Resources/SFX/Click.mp3"),
    ///     () => DoInMeeting());
    /// </code>
    /// </summary>
    public class MeetingAbilityButton : RoleButtonBase
    {
        private ActionButton _actionButton;
        private PassiveButton _passiveButton;
        private bool _meetingActive;
        private MeetingHud _meeting;
        private Vector3? _position;
        private int _slot;
        private bool _createdOnce;
        public MeetingCorner Corner { get; set; } = MeetingCorner.BottomRight;

        /// <summary>
        /// 按钮在会议界面中的位置。
        ///
        /// ⚠️⚠️ 2026-10-10 用户报「会议按钮偏了」（截图里跑到屏幕右下角**外面**去了 ✗）
        ///   根因：原来用的是**猜的常数**（x=5.1 / y=-2.2 ✗），`MeetingHud` 的坐标系与猜的不一致 ✓
        ///   ★ 改法（不再猜 ✓）：**锚定原版真实存在的底部元素** ——
        ///     `MeetingHud.TimerText`（"距离投票结束还有：N 秒"那一行 ✓ 它本来就在右下角 ✓）
        ///     → 我们的按钮放在它**上方** ✓、与它**同一列** ✓，多个按钮按 slot 往上排 ✓
        ///     拿不到它时才退回常数（兜底 ✓）
        /// </summary>
        public Vector3 Position
        {
            get
            {
                if (_position != null) return _position.Value;

                float x = Corner == MeetingCorner.BottomRight ? 4.35f : -4.35f;   // 兜底
                float y = -1.75f;

                try
                {
                    // ★★ 2026-10-10 第三次调整（用户："更歪了…你要不也试试用 Hud 的自动排列" ✓）
                    //   前两次都是"锚定会议界面里的某个元素 + 猜偏移" ✗ —— 结果一次跑到屏幕外 ✗、
                    //   一次跑到面板中间 ✗ → 说明会议界面的坐标系我们没法靠猜对上 ✓
                    //
                    //   ★ 最稳的做法：**直接问 HUD 的原版技能按钮**（`HudManager.Instance.AbilityButton`）——
                    //     它就在"右下角按钮位" ✓，是原版自己排出来的位置 ✓ 一个像素都不用猜 ✓✓
                    //     → 把会议按钮摆在它**右边一格** ✓（同一行 ✓）
                    var hud = HudManager.Instance;
                    var hudButton = hud != null ? hud.AbilityButton : null;
                    if (hudButton != null && hudButton.transform.parent != null && _meeting != null
                        && hudButton.transform.parent == _meeting.transform)
                    {
                        // 同一个父节点 → 直接复用它算好的 localPosition ✓
                        var lp = hudButton.transform.localPosition;
                        x = lp.x + (Corner == MeetingCorner.BottomRight ? 1.0f : -1.0f) * (1f + _slot);
                        y = lp.y;
                    }
                    else
                    {
                        // 父节点不同就直接抄"按钮位"的**世界坐标**再换回会议根节点空间 ✓
                        if (hudButton != null && _meeting != null)
                        {
                            var world = hudButton.transform.position;
                            var local = _meeting.transform.InverseTransformPoint(world);
                            x = local.x + (Corner == MeetingCorner.BottomRight ? 1.0f : -1.0f) * (1f + _slot);
                            y = local.y;
                        }
                    }
                }
                catch { }

                return new Vector3(x, y, -1.5f);
            }
            set => _position = value;
        }

        /// <summary>按钮旋转（默认 0）。</summary>
        public Quaternion Rotation { get; set; } = Quaternion.identity;

        private MeetingAbilityButton(RuntimeRoleTemplate role, Player player, RoleButtonConfig config, Action onClick)
            : base(role, player, config, onClick)
        {
            _config.AllowDuringMeeting = true;
            if (ReferenceEquals(_config.CanUse, RoleButtonConfig.DefaultCanUse)) _config.CanUse = () => true;
            if (ReferenceEquals(_config.CanRunCooldown, RoleButtonConfig.DefaultCanRunCooldown)) _config.CanRunCooldown = _ => true;
        }

        /// <summary>创建会议右下角按钮并注册到管理器。</summary>
        public static MeetingAbilityButton Create(RuntimeRoleTemplate role, RoleButtonConfig config, Action onClick,
            MeetingCorner corner = MeetingCorner.BottomRight)
        {
            try
            {
                var button = new MeetingAbilityButton(role, role.MyPlayer, config, onClick);
                button.Corner = corner;
                RoleButtonManager.Register(button);
                return button;
            }
            catch (Exception ex)
            {
                LightLogger.LogError("[MeetingAbilityButton.Create]", ex);
                return null;
            }
        }

        // ---- 会议生命周期（由 RoleButtonManager 的 MeetingHud patch 驱动）----

        internal void OnMeetingStart(MeetingHud meeting)
        {
            try
            {
                if (IsDeadObject || !_player.IsLocal || meeting == null) return;
                _meetingActive = true;
                _meeting = meeting;
                _slot = RoleButtonManager.MeetingSlot(this, Corner);

                // ⚠️ 不用 `PlayerControl.LocalPlayer?.Data?.IsDead`（AGENTS §4.6.1）：
                //   LocalPlayer 是 Unity 对象，假 null 时 `?.` 挡不住、访问 Data 会抛 ✗
                var local = PlayerControl.LocalPlayer;
                if (local == null) return;
                if (local.Data != null && local.Data.IsDead) return;

                if (_gameObject != null) return; // 已创建

                // ⚠️ 同理不用 `HudManager.Instance?.AbilityButton`
                var hud = HudManager.Instance;
                var template = hud != null ? hud.AbilityButton : null;
                if (template == null) return;
                if (template.transform.parent == null) return;

                _gameObject = Object.Instantiate(template.gameObject, meeting.transform);
                _gameObject.name = $"MeetingAbility_{_config.ResolvedLabel}";
                _gameObject.transform.localPosition = Position;
                _gameObject.transform.localRotation = Rotation;

                _actionButton = _gameObject.GetComponent<ActionButton>();
                _passiveButton = _gameObject.GetComponent<PassiveButton>();

                // 克隆材质，避免与原版按钮互相覆盖冷却进度
                if (_actionButton != null && _actionButton.graphic != null && _actionButton.graphic.material != null)
                    _actionButton.graphic.material = new Material(_actionButton.graphic.material);

                ApplyMeetingConfig();
                PrepareVisuals();
                if (!_createdOnce && _config.StartOnCooldown) StartCooldown();
                _createdOnce = true;
                foreach (var aspect in _gameObject.GetComponentsInChildren<AspectPosition>(true)) aspect.enabled = false;

                if (_passiveButton != null)
                {
                    _passiveButton.OnClick = new Button.ButtonClickedEvent();
                    _passiveButton.OnMouseOver = new UnityEvent();
                    _passiveButton.OnMouseOut = new UnityEvent();
                    _passiveButton.OnClick.AddListener((UnityAction)HandleClick);
                }

                if (_actionButton != null)
                {
                    if (HasLimitedUses) _actionButton.SetUsesRemaining(_usesLeft);
                    else _actionButton.SetInfiniteUses();
                }

                _gameObject.SetActive(true);
                Update();
            }
            catch (Exception ex)
            {
                LightLogger.LogError("[MeetingAbilityButton] OnMeetingStart", ex);
            }
        }

        internal void OnMeetingEnd()
        {
            _meetingActive = false;
            _meeting = null;
            DestroyUI();
        }

        private void ApplyMeetingConfig()
        {
            if (_actionButton == null) return;
            if (_config.Icon != null)
            {
                _actionButton.graphic.sprite = _config.Icon;
                // ★ 换图后必须重算冷却遮罩的 UV 包围盒 —— 否则遮罩会按**旧图标的 uv** 画，
                //   看起来就是"装填动画错误"（根因详见 AbilityButton.ApplyIcon 的注释）
                try { _actionButton.graphic.SetCooldownNormalizedUvs(); }
                catch { }
            }
            string label = _config.ResolvedLabel;
            if (!string.IsNullOrEmpty(label)) _actionButton.OverrideText(label);
            if (_config.Cooldown > 0f)
            {
                _actionButton.SetCoolDown(0f, _config.Cooldown);
                if (_actionButton.cooldownTimerText != null)
                    _actionButton.cooldownTimerText.gameObject.SetActive(false);
            }
        }

        // ---- RoleButtonBase 接口 ----

        /// <summary>会议按钮无常驻 UI（由 OnMeetingStart 创建），覆写为空。</summary>
        protected override void CreateUI()
        {
            // 无操作：会议按钮在 MeetingHud.Start 后由 OnMeetingStart 创建
        }

        protected override void DestroyUI()
        {
            if (_gameObject != null)
            {
                Object.Destroy(_gameObject);
                _gameObject = null;
            }
            _actionButton = null;
            _passiveButton = null;
        }

        /// <summary>会议期间可见；非会议期间隐藏。</summary>
        protected override bool ShouldShow
            => _meetingActive && _gameObject != null && _player.IsLocal && !_player.IsDead
                && _config.CanShow();

        protected override bool ShouldBeUsable => _meeting != null
            && (_meeting.state == MeetingHud.MeetingStates.Discussion || _meeting.state == MeetingHud.MeetingStates.NotVoted
                || _meeting.state == MeetingHud.MeetingStates.Voted) && base.ShouldBeUsable;
        protected override bool ShouldTickCooldown() => _meetingActive && _meeting != null && !_player.IsDead;
    }
}
