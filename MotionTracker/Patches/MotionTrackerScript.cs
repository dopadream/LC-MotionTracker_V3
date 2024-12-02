using LethalLib.Modules;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;
using static UnityEngine.GraphicsBuffer;
using Vector3 = UnityEngine.Vector3;

namespace MotionTracker.Patches;

public struct ScannedEntity
{
    public Collider obj;
    public Vector3 position;
    public Vector3 rawPosition;
    public float speed;
    public GameObject blip;
    public AudioSource trackerAudio;
    public AudioSource trackerBlipAudio;

}

public class MotionTrackerScript : GrabbableObject
{
    private GameObject baseRadar;
    private GameObject baseRadarOff, baseBackground;
    private GameObject LED;
    private GameObject blip;
    private GameObject blipParent;
    private AudioSource trackerAudio;
    private AudioSource trackerBlipAudio;

    private static AudioClip trackerOnClip, trackerOffClip, trackerBlipClip, trackerOutOfBatteriesClip;

    private float searchRadius = MotionTrackerConfig.MotionTrackerRange;

    private Dictionary<int, ScannedEntity> scannedEntities = new Dictionary<int, ScannedEntity>();

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
        itemProperties.weight = MotionTrackerConfig.MotionTrackerWeight;
        itemProperties.highestSalePercentage = 80;
        grabbable = true;
        grabbableToEnemies = true;
        mainObjectRenderer = GetComponent<MeshRenderer>();
        trackerAudio = GetComponent<AudioSource>();
        useCooldown = 1f;
        insertedBattery = new Battery(false, 1);
        itemProperties.batteryUsage = MotionTrackerConfig.MotionTrackerBatteryDuration;
        baseRadar = transform.Find("Canvas/BaseRadar").gameObject;
        baseRadarOff = transform.Find("Canvas/BaseRadar_off").gameObject;
        baseBackground = transform.Find("Background/Background_1").gameObject;

        LED = transform.Find("LED").gameObject;
        trackerBlipAudio = LED.GetComponent<AudioSource>();

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
        LED.GetComponent<MeshRenderer>().enabled = enable;

        if (!isPocketed)
        {
            baseRadarOff.SetActive(!enable);
            baseRadar.SetActive(enable);
            baseBackground.SetActive(true);
        }
        else
        {
            baseRadarOff.SetActive(false);
            baseRadar.SetActive(false);
            baseBackground.SetActive(false);
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
        trackerBlipAudio.pitch = 1;
        if (used)
        {
            trackerBlipAudio.PlayOneShot(trackerOnClip);
            RoundManager.Instance.PlayAudibleNoise(base.transform.position, 7f, 0.4f, 0, isInElevator && StartOfRound.Instance.hangarDoorsClosed);
        }
        else
        {
            trackerBlipAudio.PlayOneShot(trackerOffClip);
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
        EnemyAI[] array = FindObjectsOfType<EnemyAI>();

        base.Update();

        if (isPocketed)
        {
            Enable(false);
            return;
        }

        if (!isBeingUsed || insertedBattery.empty)
        {
            Enable(false);
            return;
        }

        Enable(true);
        blipParent.transform.localRotation = UnityEngine.Quaternion.Euler(0, 0, baseRadar.transform.eulerAngles.y);

        foreach (var blip in blipPool)
        {
            blip.SetActive(false);
        }

        var lastScannedEntitiesCopy = new Dictionary<int, ScannedEntity>(scannedEntities);

        scannedEntities.Clear();

        var playerPos = transform.position;
        var colliderCount = Physics.OverlapCapsuleNonAlloc(playerPos, playerPos + Vector3.down * 100, searchRadius, colliders, layerMask: 8 | 524288);

        float closestDistance = float.MaxValue;

        for (int c = 0; c < colliderCount; c++)
        {
            var collider = colliders[c];
            int hash = collider.transform.GetHashCode();

            var entity = new ScannedEntity
            {
                obj = collider,
                position = collider.transform.position - baseRadar.transform.position,
                rawPosition = collider.transform.position,
                speed = lastScannedEntitiesCopy.ContainsKey(hash)
                    ? (collider.transform.position - lastScannedEntitiesCopy[hash].rawPosition).magnitude
                    : 0
            };

            if (!scannedEntities.ContainsKey(hash))
            {
                scannedEntities.Add(hash, entity);
                var blip = blipPool[scannedEntities.Count - 1];
                entity.blip = blip;

                blip.transform.localPosition = new Vector3(
                    Remap(entity.position.x, -searchRadius, searchRadius, -45, 45),
                    Remap(entity.position.z, -searchRadius, searchRadius, -45, 45),
                    -.1f
                );

                bool isActive = entity.speed > MotionTrackerConfig.MotionTrackerSpeedDetect &&
                                ((!isHeld || Vector3.Distance(entity.rawPosition, playerHeldBy.transform.position) > 5));
                blip.SetActive(isActive);


                foreach (EnemyAI target in array)
                {
                    float distance = Vector3.Distance(target.transform.position, baseRadar.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                    }
                }

                float clampedDistance = Mathf.Clamp(closestDistance, 1f, searchRadius);

                if (blip.activeSelf && !trackerBlipAudio.isPlaying)
                {
                    float pitch = Mathf.Lerp(1.8f, 0.8f, (clampedDistance - 1f) / (searchRadius - 1f));
                    trackerBlipAudio.pitch = pitch;
                    trackerBlipAudio.PlayOneShot(trackerBlipClip);
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
