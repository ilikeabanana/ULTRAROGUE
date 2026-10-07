using System;
using System.Collections.Generic;
using System.Text;
using ThornClient.Core.ConfigurableElements;
using ThornClient.HUD;
using Ultrarogue.Characters;

namespace Ultrarogue.Thorn_Modules
{
    
    public class UIModuleStats : TextHudModule
    {
        Setting<bool> ShowDamage;
        Setting<bool> ShowAtkSpeed;
        Setting<bool> ShowCD;
        Setting<bool> ShowSpeed;
        Setting<bool> ShowJump;
        Setting<bool> ShowLuck;
        Setting<bool> ShowFloor;
        Setting<bool> ShowKeys;
        Setting<bool> ShowGold;
        public UIModuleStats() : base("ultrarogue.ui.stats", "Stats UI", "Configure the UI used to show the stats")
        {
            ShowDamage = CreateSetting<bool>("stats.ultrarogue.damage", "Show Damage", "Shows the damage stat", true);
            ShowAtkSpeed = CreateSetting<bool>("stats.ultrarogue.atkspd", "Show Attack Speed", "Shows the attack speed stat", true);
            ShowCD = CreateSetting<bool>("stats.ultrarogue.cd", "Show Cooldown Reduction", "Shows the cooldown reduction stat", true);
            ShowSpeed = CreateSetting<bool>("stats.ultrarogue.spd", "Show Speed", "Shows the speed stat", true);
            ShowJump = CreateSetting<bool>("stats.ultrarogue.jmp", "Show Jump Height", "Shows the jump height stat", false);
            ShowLuck = CreateSetting<bool>("stats.ultrarogue.luck", "Show Luck", "Shows the luck stat", true);
            ShowGold = CreateSetting<bool>("stats.ultrarogue.gold", "Show Gold", "Shows how much gold you have", true);
            ShowKeys = CreateSetting<bool>("stats.ultrarogue.keys", "Show Keys", "Shows how many keys you have", true);
            ShowFloor = CreateSetting<bool>("stats.ultrarogue.floor", "Show Floor", "Shows the floor you are on", true);
        }

        public override void OnUpdate()
        {
            
            base.OnUpdate();
            if (RogueDifficultyManager.Instance == null) return;
            if (NewMovement.Instance == null) return;
            if (Plugin.AttackSpeed == null) return;
            StringBuilder sb = new StringBuilder();
            Plugin.Logger.LogInfo("[Stats] OnUpdate tick"); // remove after testing

            float speed = NewMovement.Instance.walkSpeed;
            float baseSpeed = Plugin.Instance.normalMoveSpeed;

            float speedMult = speed / baseSpeed;

            // Attack speed
            float atkSpeed = Plugin.AttackSpeed.CalculateChanges(1f);

            // Jump Height
            float jumpHeight = NewMovement.Instance.jumpPower;
            float basejumpHeight = Plugin.Instance.normalJumpHeight;

            float jumpMult = jumpHeight / basejumpHeight;

            // Damage
            float dmg = Plugin.globalDamageMult.CalculateChanges(1f);

            // Cooldown
            float cd = Plugin.cooldownReduction.CalculateChanges(1f);

            if (ShowSpeed.Value)
                sb.Append($"SPEED: x{speedMult:F2}\n");
            if (ShowDamage.Value)
                sb.Append($"DAMAGE: x{dmg:F2}\n");
            if (ShowJump.Value)
                sb.Append($"JUMP: x{jumpMult:F2}\n");
            if (ShowAtkSpeed.Value && Plugin.SelectedChar.GetType() != typeof(Filth))
                sb.Append($"ATK-SPD: x{atkSpeed:F2}\n");
            if (ShowCD.Value)
                sb.Append($"COOLDOWN: x{cd:F2}\n");
            if (ShowLuck.Value)
                sb.Append($"LUCK: {Plugin.luck}\n");
            if (ShowGold.Value)
                sb.Append($"GOLD: {RogueDifficultyManager.Instance.Gold}\n");
            if (ShowKeys.Value)
                sb.Append($"KEYS: {RogueDifficultyManager.Instance.Keys}\n");
            if (ShowFloor.Value)
                sb.Append($"FLOOR: {RogueDifficultyManager.Instance.floor}");

            Text = sb.ToString();

        }
    }
}
