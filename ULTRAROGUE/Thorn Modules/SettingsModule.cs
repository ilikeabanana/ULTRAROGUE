using System;
using System.Collections.Generic;
using System.Text;
using ThornClientModules;
using ThornClient;
using ThornClient.Core;
using ThornClient.Core.ConfigurableElements;
using UnityEngine;
namespace Ultrarogue.Thorn_Modules
{
    
    public class SettingsModule : Module
    {
        public override Sprite Icon => Plugin.getItem("Iron Sights").ItemIcon;

        public static Setting<bool> DestroyChestsOnOpen;
        public static Setting<bool> CoinPickups;
        public static Setting<bool> AutoActive;
        public static Setting<KeyCode> ActiveKeyCode; 
        public SettingsModule() : base("ultrarogue.settings", "ULTRAROGUE SETTINGS", "To change settings for ULTRAROGUE", ModuleCategory.Gameplay, hasToggling: false)
        {
            DestroyChestsOnOpen = CreateSetting<bool>("chest.ultrarogue.opendestroy", "Destroy chests when opened", "When active, this will destroy chests after some time when you opened them.", false);
            CoinPickups = CreateSetting<bool>("pickup.ultrarogue.coinpickups", "Pickups", "When active, you have to manually pick up room pickup rewards", false);
            AutoActive = CreateSetting<bool>("actives.ultrarogue.autoactive", "Auto Activate Active", "When active, automatically activate your active item", true);
            ActiveKeyCode = CreateSetting<KeyCode>("actives.ultrarogue.activate", "Activate active key", "Key you use to activate your active.", KeyCode.C);
        }
    }
}
