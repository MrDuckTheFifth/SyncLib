# SyncLib
Registering custom NetworkPrefabs is a nightmare to deal with, since the process was originally meant to be done in the Unity editor itself.

This mod enables modders to easily register their own prefabs as NetworkPrefabs in both the server, and client with just a few lines of code. SyncLib also automatically take care of assigning a Hash to each custom prefab on it's own by having the server keep track of which custom NetworkPrefabs have which Hashes.

> [!NOTE]
> When talking about NetworkPrefabs, the "Hash" refers to it's item identifier. For example, a backpack could have a Hash of '1362' and if you wanted to spawn an instance of that backpack item, you would effectively "Spawn Hash 1362." This is also how the server saves NetworkPrefabs and their location/data.
