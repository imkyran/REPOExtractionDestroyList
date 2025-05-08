using BepInEx;
using BepInEx.Logging;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using System;
using TMPro;
using BepInEx.Configuration;
using System.Collections;
using System.Collections.Generic;

namespace REPOExtractionDestroyList;


internal static class MyPluginInfo
{
    public const string PLUGIN_GUID = "imkyran.REPOExtractionDestroyList";
    public const string PLUGIN_NAME = "REPO Extraction Destroy List";
    public const string PLUGIN_VERSION = "1.0.4";
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

    public static ConfigEntry<bool> configEnableDestroyList;
    public static ConfigEntry<int> configYOffest;

    private void Awake()
    {
        harmony.PatchAll(typeof(Plugin));

        configEnableDestroyList = Config.Bind
        (
            "ExtractionDestroyList",
            "Enable Destroy List",
            true,
            "Enables the destroy list"
        );

        configYOffest = Config.Bind
        (
            "ExtractionDestroyList",
            "UI Y Offset",
            75,
            new ConfigDescription("Moves the UI up or down", new AcceptableValueRange<int>(75, 200))
        );
    }

    [HarmonyPatch(typeof(EnergyUI), "Update")]
    [HarmonyPostfix]
    public static void EnergyUI_Update_Postfix(EnergyUI __instance)
    {
        if (__instance.GetComponent<TextMeshProUGUI>() != null)
        {
            var ___Text = __instance.GetComponent<TextMeshProUGUI>();
            if (SemiFunc.RunIsLevel() && configEnableDestroyList.Value)
            {
                ___Text.enableWordWrapping = false;
                ___Text.text = string.Concat(new string[]
                {
                        $"<line-height={configYOffest.Value}%>",
                        ___Text.text,
                        ItemDestroyListAsString(FilterColliderList(GetAllCollidersInExtraction())),
                        "</b>"
                });
            }
        }
    }

    [HarmonyPatch(typeof(RoundDirector), "ExtractionPointsLock")]
    [HarmonyPostfix]
    public static void RoundDirector_ExtractionPointsLock_Postfix(GameObject __0)
    {
        if (RoundDirector.instance == null)
            return;

        var point = __0.GetComponent<ExtractionPoint>();
        if (point != null && SemiFunc.RunIsLevel() && point == (ExtractionPoint)HarmonyLib.AccessTools.Field(typeof(RoundDirector), "extractionPointCurrent").GetValue(RoundDirector.instance))
            point.StartCoroutine(SetupFixedCollidersOnExtraction(point));
    }



    internal static bool canSetupFixedColliders = true;
    internal static IEnumerator SetupFixedCollidersOnExtraction(ExtractionPoint point)
    {
        if (!canSetupFixedColliders)
            yield break;
        else
            canSetupFixedColliders = false;

        yield return new WaitForSeconds(3.73f);

        if (point?.gameObject?.transform?.Find("Scale/Extraction Tube/Hurt Colliders Side") == null)
        {
            canSetupFixedColliders = true;
            Debug.LogError($"Failed to setup extraction tube colliders on: {point.name}");
            yield break;
        }

        var ExtractionTubeParent = point.gameObject.transform.Find("Scale/Extraction Tube");
        var SideHurtColliders = point.gameObject.transform.Find("Scale/Extraction Tube/Hurt Colliders Side");
        var NewSideColliders = GameObject.Instantiate(SideHurtColliders.gameObject, ExtractionTubeParent);
        NewSideColliders.name = "Hurt Colliders Fake";
        GameObject.Destroy(NewSideColliders.transform.Find("Hurt Collider Enemies").gameObject);

        NewSideColliders.transform.localPosition = new Vector3(0f, NewSideColliders.transform.localPosition.y - 3.2f, 0f);
        foreach (HurtCollider collider in NewSideColliders.GetComponentsInChildren<HurtCollider>())
        {
            collider.transform.localScale = new Vector3(collider.transform.localScale.x, collider.transform.localScale.y * 2, collider.transform.localScale.z);
            collider.gameObject.SetActive(false);
        }
        yield return new WaitForSeconds(5f);
        canSetupFixedColliders = true;
    }
    internal static string ItemDestroyListAsString(List<Collider> colliders)
    {
        var return_string = string.Empty;
        foreach (var collider in colliders)
        {
            var itemName = ItemNameFromCollider(collider);
            if (itemName == null) continue;
            if (return_string == string.Empty) return_string = $"\n<color=#FF2525><size=20>Destroy List:</size></color><line-height=45%>";

            return_string += $"\n<color=#FF2525><size=16>{itemName}</size></color><line-height=35%>";
        }
        return return_string;
    }

    internal static string ItemNameFromCollider(Collider collider)
    {
        PlayerAvatar playerAvatar = collider.gameObject.GetComponentInParent<PlayerAvatar>();
        if (!playerAvatar)
        {
            PlayerController componentInParent = collider.gameObject.GetComponentInParent<PlayerController>();
            if (componentInParent)
            {
                playerAvatar = componentInParent.playerAvatarScript;
            }
        }

        if (playerAvatar != null)
            return SemiFunc.PlayerGetName(playerAvatar);

        var player_tumble = collider.gameObject.GetComponentInParent<PlayerTumble>();
        if (player_tumble != null && player_tumble.playerAvatar != null) return SemiFunc.PlayerGetName(player_tumble.playerAvatar);

        var phys_obj = collider.gameObject.GetComponentInParent<PhysGrabObject>();
        if (phys_obj == null) return null;

        var enemy_rb = collider.gameObject.GetComponentInParent<EnemyRigidbody>();
        if (enemy_rb != null)
        {
            var EnemyParent = enemy_rb.gameObject.GetComponentInParent<EnemyParent>();
            if (EnemyParent != null)
            {
                return EnemyParent.enemyName;
            }
            return null;
        }

        return phys_obj.gameObject.name
            .Replace("Cart Medium", "Cart")
            .Replace("Cart Small", "Pocket Cart")
            .Replace("Enemy Valuable", "Enemy Orb")
            .Replace("Surplus Valuable", "Money Bag")
            .Replace("Item ", "")
            .Replace("Valuable ", "")
            .Replace("(Clone)", "");
    }

    internal static List<Collider> FilterColliderList(List<Collider> colliders)
    {
        var collider_names = new List<string>();
        var colliders_list = new List<Collider>();
        foreach (var collider in colliders)
        {
            var name = ItemNameFromCollider(collider);
            if (name != null && !collider_names.Contains(name))
            {
                collider_names.Add(name);
                colliders_list.Add(collider);
            }
        }
        return colliders_list;
    }

    internal static List<Collider> GetAllCollidersInExtraction()
    {
        List<Collider> colliderList = new List<Collider>();
        if (RoundDirector.instance != null)
        {
            var temp_obj = typeof(RoundDirector).GetField("extractionPointCurrent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(RoundDirector.instance);
            if (temp_obj == null) return colliderList; // no extraction point found

            ExtractionPoint currentExtraction = temp_obj as ExtractionPoint;
            foreach (HurtCollider collider in currentExtraction.GetComponentsInChildren<HurtCollider>(true))
            {
                if (collider.physImpact != HurtCollider.BreakImpact.None && collider.GetComponent<BoxCollider>()) // find hurt collider that will break phys objects
                {
                    var BoxCollider = collider.GetComponent<BoxCollider>();
                    Vector3 center = BoxCollider.bounds.center;
                    Vector3 halfExtents = BoxCollider.size * 0.5f;
                    halfExtents.x *= Mathf.Abs(collider.transform.lossyScale.x);
                    halfExtents.y *= Mathf.Abs(collider.transform.lossyScale.y);
                    halfExtents.z *= Mathf.Abs(collider.transform.lossyScale.z);

                    LayerMask HurtColliderLayer = SemiFunc.LayerMaskGetPhysGrabObject() + LayerMask.GetMask(new string[]
                    {
                            "Player"
                    }) + LayerMask.GetMask(new string[]
                    {
                            "Default"
                    }) + LayerMask.GetMask(new string[]
                    {
                            "Enemy"
                    });

                    var intersectingColliders = Physics.OverlapBox(center, halfExtents, collider.transform.rotation, HurtColliderLayer, QueryTriggerInteraction.Collide);
                    foreach (Collider collider1 in intersectingColliders)
                    {
                        if ((collider1.gameObject.CompareTag("Phys Grab Object") || collider1.gameObject.CompareTag("Player")) && !colliderList.Contains(collider1))
                            colliderList.Add(collider1);
                    }
                }
            }

        }
        return colliderList;
    }
}
