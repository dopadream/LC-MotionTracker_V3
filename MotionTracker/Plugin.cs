using BepInEx;
using LethalLib.Modules;
using UnityEngine;
using MotionTracker.Patches;
using Unity.Netcode;
using HarmonyLib;

namespace MotionTracker
{
    [BepInPlugin("Entity378.MotionTracker-V2", "MotionTracker-V2", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static Item motionTrackerLED_Item;
        private static MotionTrackerScript spawnedMotionTracker;

        private void Awake()
        {
            Logger.LogInfo($"Plugin {"Entity378.MotionTracker-V2"} is loaded!");
            MotionTrackerConfig.LoadConfig(Config);
            Logger.LogInfo("Config loaded");

            Harmony.CreateAndPatchAll(typeof(MotionTrackerConfig));

            AssetBundle assetBundle = AssetBundle.LoadFromMemory(MotionTrackerResource.motiontrackerled);
            motionTrackerLED_Item = assetBundle.LoadAsset<Item>("assets/MotionTrackerItem.asset");

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
