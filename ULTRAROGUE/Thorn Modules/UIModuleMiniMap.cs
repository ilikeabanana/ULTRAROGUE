using System.Collections;
using System.Collections.Generic;
using System.Text;
using ThornClient.HUD;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ultrarogue.Thorn_Modules
{
    
    public class UIModuleMiniMap : FramedHudModule
    {
        public UIModuleMiniMap() : base("ultrarogue.minimap.module", "Minimap UI", "Adds the minimap UI")
        {
        }

        static GameObject MinimapUI = null;

        protected override GameObject CreateContentObject()
        {
            if (RogueDifficultyManager.Instance == null) return null;
            Plugin.Logger.LogInfo($"[Minimap] CreateContentObject, floor={RogueDifficultyManager.Instance?.floor}");
            if (MinimapUI == null)
            {
                MinimapUI = Addressables.LoadAssetAsync<GameObject>(AssetsManager.RoguePath + "Minimap.prefab").WaitForCompletion();
                if (MinimapUI == null) return null;
            }
            Plugin.Instance.StartCoroutine(geheheh());
            return Object.Instantiate(MinimapUI);
        }


        IEnumerator geheheh()
        {
            yield return new WaitForSeconds(0.25f);
            RoomGenerator.Instance.SetupMinimap();
        }
    }
}
