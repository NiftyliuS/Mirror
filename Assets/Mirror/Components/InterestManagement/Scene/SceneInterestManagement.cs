using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Mirror
{
    public class SceneInterestManagement : InterestManagement
    {
        // Use Scene instead of string scene.name because when additively loading multiples of a subscene the name won't be unique
        private readonly Dictionary<Scene, HashSet<NetworkIdentity>> sceneObjects =
            new Dictionary<Scene, HashSet<NetworkIdentity>>();
        private readonly Dictionary<NetworkIdentity, Scene> lastObjectScene = new Dictionary<NetworkIdentity, Scene>();

        private HashSet<Scene> dirtyScenes = new HashSet<Scene>();

        public override void OnSpawned(NetworkIdentity identity)
        {
            Scene currentScene = gameObject.scene;
            lastObjectScene[identity] = currentScene;
            // Debug.Log($"SceneInterestManagement.OnSpawned({gameObject.name}) currentScene: {currentScene}");
            if (!sceneObjects.TryGetValue(currentScene, out HashSet<NetworkIdentity> objects))
            {
                objects = new HashSet<NetworkIdentity>();
                sceneObjects.Add(currentScene, objects);
            }

            objects.Add(identity);
        }

        public override void OnDestroyed(NetworkIdentity identity)
        {
            Scene currentScene = lastObjectScene[identity];
            lastObjectScene.Remove(identity);
            if (sceneObjects.TryGetValue(currentScene, out HashSet<NetworkIdentity> objects) && objects.Remove(identity))
                RebuildSceneObservers(currentScene);
        }

        void Update()
        {
            // only on server
            if (!NetworkServer.active) return;

            foreach (var netIdentity in NetworkIdentity.spawned.Values)
            {

                Scene currentScene = lastObjectScene[netIdentity];
                Scene newScene = netIdentity.gameObject.scene;
                if (newScene == currentScene) continue;

                // Mark new/old scenes as dirty so they get rebuilt
                dirtyScenes.Add(currentScene);
                dirtyScenes.Add(newScene);

                // This object is in a new scene so observers in the prior scene
                // and the new scene need to rebuild their respective observers lists.

                // Remove this object from the hashset of the scene it just left
                sceneObjects[currentScene].Remove(netIdentity);

                // Set this to the new scene this object just entered
                lastObjectScene[netIdentity] = newScene;

                // Make sure this new scene is in the dictionary
                if (!sceneObjects.ContainsKey(newScene))
                    sceneObjects.Add(newScene, new HashSet<NetworkIdentity>());

                // Add this object to the hashset of the new scene
                sceneObjects[newScene].Add(netIdentity);
            }

            foreach (Scene dirtyScene in dirtyScenes)
            {
                RebuildSceneObservers(dirtyScene);
            }

            dirtyScenes.Clear();
        }

        void RebuildSceneObservers(Scene scene)
        {
            foreach (NetworkIdentity netIdentity in sceneObjects[scene])
                if (netIdentity != null)
                    NetworkServer.RebuildObservers(netIdentity, false);
        }

        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnection newObserver)
        {
            return identity.gameObject.scene == gameObject.scene;
        }

        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnection> newObservers,
            bool initialize)
        {
            if (!sceneObjects.TryGetValue(identity.gameObject.scene, out HashSet<NetworkIdentity> objects))
            {
                return;
            }

            // Add everything in the hashset for this object's current scene
            foreach (NetworkIdentity networkIdentity in objects)
                if (networkIdentity != null && networkIdentity.connectionToClient != null)
                    newObservers.Add(networkIdentity.connectionToClient);
        }
    }
}