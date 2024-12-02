using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;

namespace MotionTracker.Patches;

public struct ScannedEntity
{
    public Collider obj;
    public Vector3 position;
    public Vector3 rawPosition;
    public float speed;
    public GameObject blip;
    public AudioSource trackerAudio;

}

public class MotionTrackerScript : GrabbableObject
{
    private GameObject baseRadar;
    private GameObject baseRadarOff;
    private GameObject LED;
    private GameObject blip;
    private GameObject blipParent;
    private AudioSource trackerAudio;
    private static AudioClip trackerOnClip, trackerOffClip, trackerBlipClip, trackerOutOfBatteriesClip;

    private float searchRadius = MotionTrackerConfig.MotionTrackerRange;

    private Hashtable scannedEntities = new();

    private List<GameObject> blipPool = new List<GameObject>();

    private int maxEntities = 50;

    Collider[] colliders = new Collider[200];

    public void Awake() 
    {
        List<Item> itemsList = StartOfRound.Instance?.allItemsList?.itemsList;

        try
        {
            trackerOnClip = Plugin.assetBundle.LoadAsset("motion_detector_on", typeof(AudioClip)) as AudioClip;
            trackerOffClip = Plugin.assetBundle.LoadAsset("motion_detector_off", typeof(AudioClip)) as AudioClip;
            trackerBlipClip = Plugin.assetBundle.LoadAsset("motion_detector_ping", typeof(AudioClip)) as AudioClip;
            trackerOutOfBatteriesClip = Plugin.assetBundle.LoadAsset("FlashlightFlicker", typeof(AudioClip)) as AudioClip;
        }
        catch
        {
            return;
        }

        Item walkieTalkie = itemsList.FirstOrDefault(item => item.name == "WalkieTalkie");
        Item shotGun = itemsList.FirstOrDefault(item => item.name == "Shotgun");
        Item proFlashlight = itemsList.FirstOrDefault(item => item.name == "ProFlashlight");

        itemProperties.verticalOffset = 0.1f;
        itemProperties.grabSFX = walkieTalkie.grabSFX;
        itemProperties.pocketSFX = walkieTalkie.pocketSFX;
        itemProperties.dropSFX = shotGun.dropSFX;
        grabbable = true;
        grabbableToEnemies = true;
        mainObjectRenderer = GetComponent<MeshRenderer>();
        trackerAudio = GetComponent<AudioSource>();
        useCooldown = 1f;
        insertedBattery = new Battery(false, 1);
        itemProperties.batteryUsage = MotionTrackerConfig.MotionTrackerBatteryDuration;
        baseRadar = transform.Find("Canvas/BaseRadar").gameObject;
        baseRadarOff = transform.Find("Canvas/BaseRadar_off").gameObject;
        LED = transform.Find("LED").gameObject;
        blipParent = transform.Find("Canvas/BlipParent").gameObject;
        blip = transform.Find("Canvas/BlipParent/Blip").gameObject;
        blip.SetActive(false);

        blipPool.Add(blip);
        for (var i = 1; i < maxEntities; ++i)
        {
            blipPool.Add(transform.Find($"Canvas/BlipParent/Blip ({i})").gameObject);
            blipPool[i].SetActive(false);
        }

        Enable(false);
    }

    private void Enable(bool enable)
    {
        LED.SetActive(enable);

        if (!isPocketed)
        {
            baseRadarOff.SetActive(!enable);
            baseRadar.SetActive(enable);
        }
        else
        {
            baseRadarOff.SetActive(false);
            baseRadar.SetActive(false);
        }

        if (!enable)
        {
            foreach (var blip in blipPool)
            {
                blip.SetActive(false);
            }
        }
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);

        if (used)
        {
            trackerAudio.PlayOneShot(trackerOnClip);
            RoundManager.Instance.PlayAudibleNoise(base.transform.position, 7f, 0.4f, 0, isInElevator && StartOfRound.Instance.hangarDoorsClosed);
        }
        else
        {
            trackerAudio.PlayOneShot(trackerOffClip);
            RoundManager.Instance.PlayAudibleNoise(base.transform.position, 7f, 0.4f, 0, isInElevator && StartOfRound.Instance.hangarDoorsClosed);
        }

        Enable(used);

        Debug.Log($"Motion tracker activate? : {used}");
    }

    public override void UseUpBatteries()
    {
        base.UseUpBatteries();
        trackerAudio.PlayOneShot(trackerOutOfBatteriesClip);
        RoundManager.Instance.PlayAudibleNoise(base.transform.position, 13f, 0.65f, 0, isInElevator && StartOfRound.Instance.hangarDoorsClosed);
        Enable(false);
    }

    public override void Update()
    {
        base.Update();

        if (isPocketed)
        {
            Enable(false);
            return;
        }

        if (isBeingUsed)
        {
            if (!isPocketed)
            {
                Enable(true);
                blipParent.transform.localRotation = Quaternion.Euler(0, 0, baseRadar.transform.eulerAngles.y + 0);
            }
            else
            {
                Enable(false);
            }

            if (insertedBattery.empty)
            {
                Enable(false);
                return;
            }

            for (var j = 0; j < maxEntities; ++j)
            {
                blipPool[j].SetActive(false);
            }

            var lastScannedEntities = new Hashtable(scannedEntities);
            scannedEntities.Clear();

            var playerPos = transform.position;

            var colliderCount = Physics.OverlapCapsuleNonAlloc(playerPos, new Vector3(playerPos.x, playerPos.y-200, playerPos.z), searchRadius, colliders,
                layerMask: 8 | 524288); // Player | Enemies


            var i = 0;
            for (int c = 0; c < colliderCount; ++c)
            {
                var collider = colliders[c];
                var entity = new ScannedEntity
                {
                    obj = collider, position = collider.transform.position - baseRadar.transform.position,
                    rawPosition = collider.transform.position
                };

                if (lastScannedEntities.Contains(entity.obj.transform.GetHashCode()))
                {
                    entity.speed = (collider.transform.position -
                                    ((ScannedEntity)lastScannedEntities[entity.obj.transform.GetHashCode()])
                                    .rawPosition)
                        .magnitude;
                }
                else
                {
                    entity.speed = 0;
                }

                if (!scannedEntities.Contains(entity.obj.transform.GetHashCode()))
                {
                    entity.blip = blipPool[i];

                    i += 1;

                    var blipLocalPos = entity.blip.transform.localPosition;
                    blipLocalPos = entity.position;

                    entity.blip.transform.localPosition = new Vector3(
                        Remap(blipLocalPos.x, -searchRadius, searchRadius, -45, 45),
                        Remap(blipLocalPos.z, -searchRadius, searchRadius, -45, 45),
                        -.1f);

                    // only enable blips for moving objects
                    entity.blip.SetActive(entity.speed > MotionTrackerConfig.MotionTrackerSpeedDetect);
                    if (entity.speed > MotionTrackerConfig.MotionTrackerSpeedDetect && !trackerAudio.isPlaying)
                    {
                        trackerAudio.PlayOneShot(trackerBlipClip);
                        RoundManager.Instance.PlayAudibleNoise(base.transform.position, 7f, 0.05f, 0, isInElevator && StartOfRound.Instance.hangarDoorsClosed);
                    }

                    scannedEntities.Add(entity.obj.transform.GetHashCode(), entity);
                }
            }
        }
    }

    public float Remap(float from, float fromMin, float fromMax, float toMin, float toMax)
    {
        var fromAbs = from - fromMin;
        var fromMaxAbs = fromMax - fromMin;

        var normal = fromAbs / fromMaxAbs;

        var toMaxAbs = toMax - toMin;
        var toAbs = toMaxAbs * normal;

        var to = toAbs + toMin;

        return to;
    }
}
