using System;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

public class MotionTrackerConfig
{
    private const int byteDim = 25;

    private static int MotionTrackerCostLocal = 90;
    private static float MotionTrackerBatteryDurationLocal = 600f;
    private static float MotionTrackerSpeedDetectLocal = 0.008f;
    private static float MotionTrackerRangeLocal = 50f;
    private static float MotionTrackerWeightLocal = 1.05f;
    private static float MotionTrackerVolumeLocal = 1f;
    private static float MotionTrackerMinPitchLocal = 1f;
    private static float MotionTrackerMaxPitchLocal = 1.3f;


    public static int MotionTrackerCost = 90;
    public static float MotionTrackerBatteryDuration = 600f;
    public static float MotionTrackerSpeedDetect = 0.008f;
    public static float MotionTrackerRange = 50f;
    public static float MotionTrackerWeight = 1.05f;
    public static float MotionTrackerVolume = 1f;
    public static float MotionTrackerMinPitch = 1f;
    public static float MotionTrackerMaxPitch = 1.3f;
    private static void SetValues(int Cost, float BatteryDuration, float SpeedDetect, float Range, float Weight, float Volume, float MinPitch, float MaxPitch)
    {
        MotionTrackerCost = Cost;
        MotionTrackerBatteryDuration = BatteryDuration;
        MotionTrackerSpeedDetect = SpeedDetect;
        MotionTrackerRange = Range;
        MotionTrackerWeight = Weight;
        MotionTrackerVolume = Volume;
        MotionTrackerMinPitch = MinPitch;
        MotionTrackerMaxPitch = MaxPitch;
    }
    private static void SetToLocalValues() => SetValues(MotionTrackerCostLocal, MotionTrackerBatteryDurationLocal, MotionTrackerSpeedDetectLocal, MotionTrackerRangeLocal, MotionTrackerWeightLocal, MotionTrackerVolumeLocal, MotionTrackerMinPitchLocal, MotionTrackerMaxPitchLocal);

    public static void LoadConfig(ConfigFile config)
    {
        Debug.Log("MotionTrackerLog CONFIG:" + config);

        MotionTrackerCostLocal = Math.Clamp(config.Bind("General", "MotionTrackerCost", 90, "Motion Tracker's cost").Value, 0, 9999);
        MotionTrackerBatteryDurationLocal = Mathf.Clamp(config.Bind("General", "MotionTrackerBatteryDuration", 600f, "Motion Tracker's battery life").Value, 0f, 9999f);
        MotionTrackerSpeedDetectLocal = Mathf.Clamp(config.Bind("General", "MotionTrackerSpeedDetect", 0.008f, "Minimum speed at which entities can be detected by the Motion Tracker (0.05 is faster than a crouch walk)").Value, 0f, 9999f);
        MotionTrackerRangeLocal = Mathf.Clamp(config.Bind("General", "MotionTrackerRange", 50f, "Motion Tracker's range of action").Value, 0f, 9999f);
        MotionTrackerWeightLocal = Mathf.Clamp(config.Bind("General", "MotionTrackerWeight", 1.05f, "The weight of the motion tracker. Formula is 1+(X/105). (default value is 5 pounds, 1.0 is 0 pounds) ").Value, 1f, 9999f);
        MotionTrackerVolumeLocal = Mathf.Clamp(config.Bind("General", "MotionTrackerVolume", 0.5f, "The volume of the motion tracker's ping sound.").Value, 0f, 1f);
        MotionTrackerMinPitchLocal = Mathf.Clamp(config.Bind("General", "MotionTrackerMinPitch", 1f, "The minimum pitch of the motion tracker's ping sound.").Value, 0f, 9999f);
        MotionTrackerMaxPitchLocal = Mathf.Clamp(config.Bind("General", "MotionTrackerMaxPitch", 1.3f, "The maximum pitch of the motion tracker's ping sound.").Value, 0f, 9999f);

        SetToLocalValues();
    }

    public static byte[] GetSettings()
    {
        byte[] data = new byte[byteDim];
        data[0] = 1;
        Array.Copy(BitConverter.GetBytes(MotionTrackerCostLocal), 0, data, 1, 4);
        Array.Copy(BitConverter.GetBytes(MotionTrackerBatteryDurationLocal), 0, data, 5, 4);
        Array.Copy(BitConverter.GetBytes(MotionTrackerSpeedDetectLocal), 0, data, 9, 4);
        Array.Copy(BitConverter.GetBytes(MotionTrackerRangeLocal), 0, data, 13, 4);
        Array.Copy(BitConverter.GetBytes(MotionTrackerWeightLocal), 0, data, 17, 4);
        Array.Copy(BitConverter.GetBytes(MotionTrackerVolumeLocal), 0, data, 21, 4);

        return data;
    }

    public static void SetSettings(byte[] data)
    {
        switch (data[0])
        {
            case 1:
                {
                    MotionTrackerCost = BitConverter.ToInt32(data, 1);
                    MotionTrackerBatteryDuration = BitConverter.ToSingle(data, 5);
                    MotionTrackerSpeedDetect = BitConverter.ToSingle(data, 9);
                    MotionTrackerRange = BitConverter.ToSingle(data, 13);
                    MotionTrackerWeight = BitConverter.ToSingle(data, 17);
                    MotionTrackerVolume = BitConverter.ToSingle(data, 21);
                    Debug.Log("MotionTrackerLog: Host config set successfully");
                    break;
                }
            default:
                {
                    throw new Exception("Invalid version byte");
                }
        }
    }

    // networking

    private static bool IsHost() => NetworkManager.Singleton.IsHost;

    public static void OnRequestSync(ulong clientID, FastBufferReader reader)
    {
        if (!IsHost()) return;

        Debug.Log("MotionTrackerLog: Sending config to client " + clientID);
        byte[] data = GetSettings();
        FastBufferWriter dataOut = new(data.Length, Unity.Collections.Allocator.Temp, data.Length);
        try
        {
            dataOut.WriteBytes(data);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("MotionTracker_OnReceiveConfigSync", clientID, dataOut, NetworkDelivery.Reliable);
        }
        catch (Exception e)
        {
            Debug.LogError("MotionTrackerLog: Failed to send config: " + e);
        }
        finally
        {
            dataOut.Dispose();
        }
    }

    public static void OnReceiveSync(ulong clientID, FastBufferReader reader)
    {
        Debug.Log("MotionTrackerLog: Received config from host");
        byte[] data = new byte[byteDim];
        try
        {
            reader.ReadBytes(ref data, byteDim);
            SetSettings(data);
        }
        catch (Exception e)
        {
            Debug.LogError("MotionTrackerLog: Failed to receive config: " + e);
            SetToLocalValues();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
    static void ServerConnect()
    {
        if (IsHost())
        {
            Debug.Log("MotionTrackerLog: Started hosting, using local settings");
            SetToLocalValues();
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("MotionTracker_OnRequestConfigSync", OnRequestSync);
        }
        else
        {
            Debug.Log("MotionTrackerLog: Connected to server, requesting settings");
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("MotionTracker_OnReceiveConfigSync", OnReceiveSync);
            FastBufferWriter blankOut = new(byteDim, Unity.Collections.Allocator.Temp);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("MotionTracker_OnRequestConfigSync", 0uL, blankOut, NetworkDelivery.Reliable);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
    static void ServerDisconnect()
    {
        Debug.Log("MotionTrackerLog: Server disconnect");
        SetToLocalValues();
    }
}
