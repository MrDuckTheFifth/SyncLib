using Alta.Crafting;
using Alta.Networking;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using static SyncLib.Items.NetworkPrefabRegistry;
using JointInsert = Alta.Crafting.JointInsert;
using JointSlot = Alta.Crafting.JointSlot;
using WorkshopChildEntity = ATT_Workshop_Utilities.ChildNetworkEntity;
using WorkshopCraftingPart = ATT_Workshop_Utilities.CraftingPart;
using WorkshopDurabilityModule = ATT_Workshop_Utilities.DurabilityModule;
using WorkshopGrabPoint = ATT_Workshop_Utilities.GrabPoint;
using WorkshopJointInsert = ATT_Workshop_Utilities.JointInsert;
using WorkshopJointSlot = ATT_Workshop_Utilities.JointSlot;
using WorkshopPickup = ATT_Workshop_Utilities.Pickup;
using WorkshopImpactorMap = ATT_Workshop_Utilities.ImpactorMap;
using WorkshopImpactor = ATT_Workshop_Utilities.Impactor;
using WorkshopImpactTool = ATT_Workshop_Utilities.ImpactTool;
using Alta.Impact;

namespace SyncLib.Items {
    public static class WorkshopApplier {
        public static void ApplyAll(NetworkEntity entity, NetworkPrefab networkprefab, ref List<Component> allComps) {
            ApplyChildEntity(entity, ref allComps);
            ApplyCraftingPart(entity, ref allComps);
            ApplyJoints(entity, networkprefab, ref allComps);
            ApplyPickup(entity, ref allComps);
            ApplyDurabilityModule(entity, ref allComps);
            ApplyImpactors(entity, ref allComps);
        }

        public static void ApplyChildEntity(NetworkEntity entity, ref List<Component> allComps) {
            WorkshopChildEntity[] childEntities = entity.GetComponentsInChildren<WorkshopChildEntity>();

            foreach (WorkshopChildEntity childEntity in childEntities) {
                GameObject originalObject = childEntity.gameObject;

                NetworkEntity actualChildEntity = originalObject.AddComponent<NetworkEntity>();

                if (!childEntity.DoNotMarkAsChild) {
                    Traverse.Create(actualChildEntity).Field("Parent").SetValue(entity);
                }

                allComps.Add(actualChildEntity);
            }
        }

        public static void ApplyCraftingPart(NetworkEntity entity, ref List<Component> allComps) {
            WorkshopCraftingPart[] craftingParts = entity.GetComponentsInChildren<WorkshopCraftingPart>();

            foreach (WorkshopCraftingPart craftingPart in craftingParts) {
                GameObject originalObject = craftingPart.gameObject;

                Type type = craftingPart.isAddon ?
                    typeof(AddonCraftingPart) :
                    typeof(Alta.Crafting.CraftingPart);

                Component comp = originalObject.AddComponent(type);

                allComps.Add(comp);
            }
        }

        public static void ApplyJoints(NetworkEntity entity, NetworkPrefab networkprefab, ref List<Component> allComps) {
            WorkshopJointSlot[] jointSlots = entity.GetComponentsInChildren<WorkshopJointSlot>();

            foreach (WorkshopJointSlot jointSlot in jointSlots) {
                GameObject originalObject = jointSlot.gameObject;

                GameObject JointSlotObj = GameObject.Instantiate(JointSlotReference);

                Transform originalTr = jointSlot.transform;
                Transform newTr = JointSlotObj.transform;

                newTr.parent = originalTr.parent;

                newTr.localPosition = originalTr.localPosition;
                newTr.localScale = originalTr.localScale;
                newTr.localRotation = originalTr.localRotation;

                JointSlot slot = JointSlotObj.GetComponent<JointSlot>();
                NetworkEntity slotEntity = JointSlotObj.GetComponent<NetworkEntity>();

                if (slot is null || slotEntity is null)
                    continue;

                slotEntity.OnValidate();


                Traverse prefabTraverse = Traverse.Create(networkprefab).Field("embeddedEntities");

                List<NetworkEntity> list = (List<NetworkEntity>)prefabTraverse.GetValue();

                list.Add(slotEntity);
                prefabTraverse.SetValue(list);


                List<JointSlotType> types = new List<JointSlotType>();

                foreach (Enums.JointSlotType type in jointSlot.JointSlotTypes) {
                    string name = type.ToString();

                    JointSlotType? jointSlotType = FindScriptableObjectOfName<JointSlotType>(name);

                    if (jointSlotType != null) {
                        types.Add(jointSlotType);
                    }
                }

                Traverse traverse = Traverse.Create(slot);

                traverse.Field("types").SetValue(types);

                if (slot.CraftingPart != null) {
                    traverse.Field("ConnectedPart").SetValue(slot.CraftingPart);
                }

                allComps.Add(slotEntity);

                originalObject.SetActive(false);
            }

            WorkshopJointInsert[] insertSlots = entity.GetComponentsInChildren<WorkshopJointInsert>();

            foreach (WorkshopJointInsert jointInsert in insertSlots) {
                GameObject originalObject = jointInsert.gameObject;

                GameObject JointInsertObj = GameObject.Instantiate(JointInsertReference);

                Transform originalTr = jointInsert.transform;
                Transform newTr = JointInsertObj.transform;

                newTr.parent = originalTr.parent;

                newTr.localPosition = originalTr.localPosition;
                newTr.localScale = originalTr.localScale;
                newTr.localEulerAngles = originalTr.localEulerAngles;

                JointInsert slot = JointInsertObj.GetComponent<JointInsert>();
                NetworkEntity slotEntity = JointInsertObj.GetComponent<NetworkEntity>();

                if (slot is null || slotEntity is null)
                    continue;

                slotEntity.OnValidate();


                Traverse traverse = Traverse.Create(networkprefab).Field("embeddedEntities");

                List<NetworkEntity> list = (List<NetworkEntity>)traverse.GetValue();

                list.Add(slotEntity);
                traverse.SetValue(list);


                string name = jointInsert.InsertType.ToString();

                JointInsertType? jointSlotType = FindScriptableObjectOfName<JointInsertType>(name);

                if (jointSlotType != null) {
                    Traverse.Create(slot).Field("type").SetValue(jointSlotType);
                }

                allComps.Add(slotEntity);

                originalObject.SetActive(false);
            }
        }

        public static void ApplyPickup(NetworkEntity entity, ref List<Component> allComps) {
            WorkshopPickup[] pickups = entity.GetComponentsInChildren<WorkshopPickup>();

            foreach (WorkshopPickup pickup in pickups) {
                GameObject originalObject = pickup.gameObject;

                Pickup comp = originalObject.AddComponent<Pickup>();

                Traverse traverse = Traverse.Create(comp);

                traverse.Field("isTightGrab").SetValue(pickup.isTightGrab);
                traverse.Field("isSwingable").SetValue(pickup.isSwingable);
                traverse.Field("isCumbersome").SetValue(pickup.isCumbersome);
                traverse.Field("blockLevel").SetValue((BlockLevel)pickup.blockLevel);
                traverse.Field("jointType").SetValue((PickUpJointType)pickup.jointType);

                Transform transform = pickup.transform;

                WorkshopGrabPoint[] wsGrabPoints = transform.GetComponentsInChildren<WorkshopGrabPoint>();

                GrabPoint[] grabPoints = new GrabPoint[wsGrabPoints.Length];

                for (int i = 0; i < wsGrabPoints.Length; i++) {
                    WorkshopGrabPoint grabPoint = wsGrabPoints[i];

                    GrabPoint gp = new GrabPoint() {
                        rotationMode = (RotationMode)grabPoint.rotationMode,
                    };

                    Traverse traverse2 = Traverse.Create(gp);

                    traverse2.Field("transform").SetValue(grabPoint.transform);
                    traverse2.Field("position").SetValue(grabPoint.transform.localPosition);
                    traverse2.Field("rotationEuler").SetValue(grabPoint.transform.localEulerAngles);
                    traverse2.Field("linear").SetValue(grabPoint.Linear);

                    grabPoints[i] = gp;
                }

                Traverse.Create(pickup).Field("grabPoints").SetValue(grabPoints);

                allComps.Add(comp);
            }
        }

        public static void ApplyDurabilityModule(NetworkEntity entity, ref List<Component> allComps) {
            WorkshopDurabilityModule[] durabilityModules = entity.GetComponentsInChildren<WorkshopDurabilityModule>();

            foreach (WorkshopDurabilityModule durabilityModule in durabilityModules) {
                GameObject originalObject = durabilityModule.gameObject;

                DurabilityModule dur = originalObject.AddComponent<DurabilityModule>();

                Traverse traverse = Traverse.Create(durabilityModule);

                traverse.Field("integrity").SetValue(durabilityModule.integrity);

                string durabilitySetting = durabilityModule.DurabilitySettings.ToString();

                DurabilitySettings? settings = FindScriptableObjectOfName<DurabilitySettings>(durabilitySetting, true);

                if (settings != null) {
                    traverse.Field("durabilitySettings").SetValue(settings);
                }

                allComps.Add(dur);
            }
        }

        public static void ApplyImpactors(NetworkEntity entity, ref List<Component> allComps) {
            WorkshopImpactTool[] impactTools = entity.GetComponentsInChildren<WorkshopImpactTool>();

            foreach (WorkshopImpactTool impactTool in impactTools) {
                GameObject originalObject = impactTool.gameObject;

                ImpactTool comp = originalObject.AddComponent<ImpactTool>();

                Traverse traverse = Traverse.Create(comp);

                traverse.Field("damage").SetValue(impactTool.damage);
                traverse.Field("blockableLayers").SetValue(impactTool.BlockableLayers);
                traverse.Field("timeToSelfHit").SetValue(impactTool.timeToSelfHit);
                traverse.Field("isMedHandle").SetValue(impactTool.isMedHandle);
                traverse.Field("singleHandedMultiplier").SetValue(impactTool.singleHandedMultiplier);
                traverse.Field("dualHandedMultiplier").SetValue(impactTool.dualHandedMultiplier);
                traverse.Field("lengthAxis").SetValue(impactTool.lengthAxis);
                traverse.Field("isIgnoringVelocityCheck").SetValue(impactTool.isIgnoringVelocityCheck);
                traverse.Field("requiredBlockLevel").SetValue((BlockLevel)impactTool.RequiredBlockLevel);
                traverse.Field("resetConditions").SetValue((int)impactTool.ResetConditions);
                traverse.Field("providesExperince").SetValue(impactTool.ProvidesExperience);

                allComps.Add(comp);
            }

            WorkshopImpactorMap[] impactorMaps = entity.GetComponentsInChildren<WorkshopImpactorMap>();

            foreach (WorkshopImpactorMap impactorMap in impactorMaps) {
                GameObject originalObject = impactorMap.gameObject;

                ImpactorMap comp = originalObject.AddComponent<ImpactorMap>();

                WorkshopImpactor[] impactors = originalObject.GetComponentsInChildren<WorkshopImpactor>();

                foreach (WorkshopImpactor impactor in impactors) {
                    GameObject originalObject2 = impactor.gameObject;

                    Impactor comp2 = originalObject2.AddComponent<Impactor>();

                    allComps.Add(comp2);
                }

                comp.OnValidate();
            }
        }
    }
}