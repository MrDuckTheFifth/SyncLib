# SyncLib

> [!WARNING]
> At the moment, the "Impactor", "ImpactTool", and "ImpactorMap" components do **NOT** work. However, the current release still contains them. If you want to find the issue and create a pull request, it would be very helpful.

This mod enables modders to easily register their own custom items/Network Prefabs in both the server, and client with just a few lines of code. SyncLib also automatically takes care of assigning a Hash to each custom prefab on it's own by having the server keep track of which custom NetworkPrefabs have which Hashes.

> [!NOTE]
> When talking about NetworkPrefabs, the "Hash" refers to it's item identifier. For example, a backpack could have a Hash of '1362' and if you wanted to spawn an instance of that backpack item, you would effectively "Spawn Hash 1362." This is also how the server saves NetworkPrefabs and their location/data.
-----------------

# Setup

First, you'll want to install the ATT Workshop Unity project, which is located here:

<img width="930" height="377" alt="image" src="https://github.com/user-attachments/assets/4b8e0baf-7da9-455c-b5af-b0d6d2d8a83a" />

> [!NOTE]
> This Unity project is required for creating and registering custom items with SyncLib.
> 
> HOWEVER, if you are simply creating a custom prefab with no item logic. You can simply use your own Unity projects with your own AssetBundles.

----------------
Once you load up the Unity project, you should see an empty scene. Multiple folders will be present inside of the Assets folder. It is highly recommended to store all of your custom items in the "Items" folder for organization.

There is also an example custom item already created in the Items folder for reference.

<img width="1917" height="967" alt="image" src="https://github.com/user-attachments/assets/be8f4e08-f798-4fcf-bae3-a104cba659c9" />

# Creating a new prefab


Lets get started with creating a custom item. For this example, I will create a cube item since I cannot be bothered to actually model something :)

Here we can just create the cube object and prepare it to turn into a prefab. You may have noticed that there are two GameObjects, a parent named "Cube Item" and a child object named "Model" This is done for one very specific reason.

All ATT items must have a scale of 1, 1, 1. Otherwise, ATT automatically scales the prefab if the player docks, and undocks the object. To get around this issue, we can create an empty parent GameObject that contains the scaled model.

> [!NOTE]
> If your model is already (1, 1, 1) you do not need to create an empty parent.

<img width="366.5" height="228.5" alt="image" src="https://github.com/user-attachments/assets/94d2ffa4-978f-4ddf-abeb-2284a6a137d4" />

<img width="255" height="277.8" alt="image" src="https://github.com/user-attachments/assets/54b3563d-47c3-4546-9d38-48f245539055" />

--------------
# Setting up the object

Now we can place our prefab into an organized folder, lets just create a "Cube" folder for this example. Next, we can assign the "Item" script to the parent GameObject aswell as a "Rigidbody" component so physics are properly applied. There are many settings you can configure, however I'm not going to go over every small setting in these components.

> [!NOTE]
> In-game, a "NetworkRigidbody" component needs to be assigned alongside the Rigidbody component. However, SyncLib automatically takes care of that.

<img width="956" height="480" alt="image" src="https://github.com/user-attachments/assets/8411da16-c222-4f98-8368-ac6a44beee45" />

-------------
The last very important thing we want for our item is the ability to pick it up. To do this, we can attach a "Pickup" script.

<img width="487.8" height="303" alt="image" src="https://github.com/user-attachments/assets/54280843-3995-45d7-b432-5293dad18d8e" />

-------------

But we aren't done yet. We still need to create grab points so the game knows where the player is allowed to grab.

Here is a good example for what this cube item specifically needs. The "surface" rotation mode allows the player to grab at any point on the object.

<img width="1915" height="470" alt="image" src="https://github.com/user-attachments/assets/636c5562-65e2-4ffd-b7fc-c4421975baa0" />

------------
If you tried this setup in-game, you may have noticed one issue. When picking up the object, your hands don't properly wrap around it. How do we fix this?

ATT has a very strict setup you need to remember in order to accomplish this. So ATT Workshop has a way to automate it. First, assign the "Setup Tight Grab" component on the parent object. This will have a few settings you **NEED** to assign, which includes the model object's transform, and the mesh. Next, you can set the "Execute" Boolean to true, and it will automatically setup a "Tight Grab" for the object. A Tight Grab just simply refers to the player's hand wrapping around the object mesh when holding it.

> [!NOTE]
> This component is purely for editor usage, it will not appear in-game.


<img width="461" height="220" alt="image" src="https://github.com/user-attachments/assets/9458aaf0-415a-4208-9109-eb761059fa33" />

------------

As you can see, a "Tight Grab" object was successfully created!
This object requires a Rigidbody that has the "IsKinematic" Boolean set to true. It also contains a Mesh Collider that purposefully has "Convex" set to true. **DO NOT TURN IT OFF!** It will simply not work otherwise.

<img width="477" height="590" alt="image" src="https://github.com/user-attachments/assets/71c569b7-d5a1-48a7-9269-5471b866a1a2" />

# Exporting it to use in mods

We've setup our new item, lets create a mod to get this in-game!

In order to export this as an Asset Bundle, we need to assign it to an AssetBundle file. You can do that by clicking on the prefab in the profile folder, and assigning a name in the bottom right corner.

<img width="471" height="281" alt="image" src="https://github.com/user-attachments/assets/e3982f9f-20c4-40d3-9f99-cf5698ddbf50" />

-----------

Now we can use ATT Workshop to export it into an AssetBundle file. The file will be available in "Assets/Exported Assets"

<img width="537" height="233" alt="image" src="https://github.com/user-attachments/assets/f172a110-5693-4438-8a5e-8a545d05776e" />

<img width="563" height="232" alt="image" src="https://github.com/user-attachments/assets/b7bcd22a-990d-4f9c-aa93-5cfd74a98915" />

----------

Once we are in our IDE, we can add the AssetBundle file to any folder, preferably a "Resources" folder for organization.

**VERY IMPORTANT!**

You need to set the "Build Action" of your AssetBundle file to "Embedded Resource." Otherwise it will simply not load in-game.

<img width="357" height="403" alt="image" src="https://github.com/user-attachments/assets/9fcfc403-8b77-4c6a-aaf1-d3a0033b2487" />

---------

The code for registering an item is as simple as this:

```csharp
using MelonLoader;
using SyncLib.Items;
using UnityEngine;
using System.Reflection;
using Alta.Inventory;
using Alta.Networking;

[assembly: MelonInfo(typeof(CubeMod.Main), "CubeMod", "The most epic version number one", "MrDuckTheFifth")]

namespace CubeMod {
    public class Main : MelonMod {
        public override void OnInitializeMelon() {
            NetworkPrefabRegistry.RegisterPrefabs += NetworkPrefabRegistry_RegisterPrefabs;
        }

        private static GameObject GetGameObjectFromAssetBundle(string embeddedPath, string objectName) {
            using (System.IO.Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(embeddedPath)) {
                AssetBundle assetBundle = AssetBundle.LoadFromStream(stream);

                GameObject obj = (GameObject)assetBundle.LoadAsset(objectName);

                return obj;
            }
        }

        private void NetworkPrefabRegistry_RegisterPrefabs() {
            // Remember to set your Asset Bundle as an "Embedded resource"!
            GameObject obj = GetGameObjectFromAssetBundle(
                embeddedPath: "CubeMod.Resources.cube",
                objectName: "Cube Item"
            );

            // This only registers it as a Network Prefab, giving it a unique HashId per server that stays consistant even when uninstalled and reinstalled.
            // For some occasions, you will only need to register it as a NetworkPrefab for structures that require network behaviours.
            NetworkPrefab? prefab = this.RegisterCustomPrefab(
                prefab: obj,
                CustomPrefabId: "Cube"
            );

            if (prefab is null) {
                // Failed to register this prefab.

                // This can mean two things:
                // - An issue occured while registering (Which will be logged)
                // - The client had this mod, while the server didn't, so it was skipped.

                return;
            }

            // It will find the Item component, and setup accordingly.
            Item? item = prefab.RegisterCustomItem();
        }
    }
}
```

# Results

Your item should be fully registered in-game now!
There may always be a few things you need to tweak about your item once you see it in-game. For example, I made my item way too big. You can always change the item in the editor and re-export whenever you need to.

<img width="639" height="360" alt="Video Project 15" src="https://github.com/user-attachments/assets/fe0bd59b-abf9-482c-b029-d47d0f228343" />
