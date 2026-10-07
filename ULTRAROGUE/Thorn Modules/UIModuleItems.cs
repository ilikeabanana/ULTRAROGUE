using ThornClient.Core.ConfigurableElements;
using ThornClient.HUD;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace Ultrarogue.Thorn_Modules
{
    
    public class UIModuleItems : FramedHudModule
    {
        static GameObject ItemsUI = null;
        Setting<int> MaxIcons;

        public UIModuleItems() : base("items.UI.ultrarogue", "Items UI", "UI to show your items")
        {
            MaxIcons = CreateSetting<int>("items.UI.ultrarogue.max.icons", "Max Icons", "The Maximum Icons that can show.", 20);
        }

        protected override GameObject CreateContentObject()
        {
            if (RogueDifficultyManager.Instance == null) return null;
            Plugin.Logger.LogInfo($"[Minimap] CreateContentObject, floor={RogueDifficultyManager.Instance?.floor}");
            if (ItemsUI == null)
            {
                ItemsUI = Addressables.LoadAssetAsync<GameObject>(AssetsManager.RoguePath + "ItemsUI.prefab").WaitForCompletion();
                if (ItemsUI == null) return null;
            }

            GameObject content = Object.Instantiate(ItemsUI);

            Transform panel = content.transform.Find("Panel");
            if (panel == null)
            {
                Plugin.Logger.LogError("ItemsUI prefab has no 'Panel' child!");
                return content;
            }

            panel.gameObject.SetActive(true);

            GridLayoutGroup grid = panel.GetComponent<GridLayoutGroup>();
            RogueDifficultyManager.Instance.SetItemParent(grid);

            return content;
        }
    }
}