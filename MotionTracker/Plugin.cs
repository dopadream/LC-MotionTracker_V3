using BepInEx;
using LethalLib.Modules;
using UnityEngine;
using MotionTracker.Patches;
using Unity.Netcode;
using HarmonyLib;
using System.IO;
using System.Reflection;

namespace MotionTracker
{
    [BepInPlugin("dopadream.MotionTracker-V3", "MotionTracker-V3", "1.0.6")]
    public class Plugin : BaseUnityPlugin
    {
        private static Item motionTrackerLED_Item;
        private static MotionTrackerScript spawnedMotionTracker;
        public static AssetBundle assetBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "motiontrackerled"));

        private void Awake()
        {
            Logger.LogInfo($"Plugin {"dopadream.MotionTracker-V3"} is loaded!");
            MotionTrackerConfig.LoadConfig(Config);
            Logger.LogInfo("Config loaded");

            Harmony.CreateAndPatchAll(typeof(MotionTrackerConfig));

            if (motionTrackerLED_Item == null)
            {
                try
                {
                    motionTrackerLED_Item = assetBundle.LoadAsset("MotionTrackerItem", typeof(Item)) as Item;
                }
                catch
                {
                    Logger.LogError("Encountered some error loading asset bundle. Did you install the plugin correctly?");
                    return;
                }
            }

            var netObj = motionTrackerLED_Item.spawnPrefab.GetComponent<NetworkObject>();

            // This seems to be necessary to fix the error when dropping the item in multiplayer
            netObj.AutoObjectParentSync = false;

            spawnedMotionTracker = motionTrackerLED_Item.spawnPrefab.AddComponent<MotionTrackerScript>();

            spawnedMotionTracker.itemProperties = motionTrackerLED_Item;

            spawnedMotionTracker.isInFactory = true;

            Items.RegisterShopItem(motionTrackerLED_Item, MotionTrackerConfig.MotionTrackerCost);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(motionTrackerLED_Item.spawnPrefab);
        }
    }
}
