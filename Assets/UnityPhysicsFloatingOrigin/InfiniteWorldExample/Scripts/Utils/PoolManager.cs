using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
    public static class PoolManager
    {
        private const int DefaultInitialSize = 5;

        private static readonly Dictionary<PoolKey, Pool> poolsByKey = new Dictionary<PoolKey, Pool>();
        private static readonly Dictionary<GameObject, Pool> poolByGameObject = new Dictionary<GameObject, Pool>();
        private static readonly Dictionary<GameObject, IPoolable[]> poolableCallbacksByGameObject = new Dictionary<GameObject, IPoolable[]>();

        public static GameObject Allocate(GameObject parent, GameObject prefab, Vector3 position, Quaternion rotation, bool persistent = false)
        {
            if (!prefab)
            {
                Debug.LogError("PoolManager.Allocate called with null prefab.");
                return null;
            }

            var key = new PoolKey(prefab, parent, persistent);

            if (!poolsByKey.TryGetValue(key, out var pool))
            {
                pool = new Pool(parent, prefab, DefaultInitialSize, persistent);
                poolsByKey.Add(key, pool);
            }

            return pool.Allocate(position, rotation);
        }

        public static GameObject Allocate(GameObject parent, GameObject prefab)
        {
            return Allocate(parent, prefab, Vector3.zero, Quaternion.identity);
        }

        public static void Deallocate(GameObject obj)
        {
            if (ReferenceEquals(obj, null))
            {
                return;
            }

            if (!poolByGameObject.TryGetValue(obj, out var pool))
            {
                UnregisterTrackedObject(obj);
                if (obj)
                {
                    UnityEngine.Object.Destroy(obj);
                }
                return;
            }

            pool.Deallocate(obj);
        }

        public static void ClearAllPools()
        {
            List<PoolKey> keysToRemove = new List<PoolKey>();

            foreach (var pair in poolsByKey)
            {
                Pool pool = pair.Value;

                if (pool.IsPersistent)
                {
                    continue;
                }

                pool.ClearAndDestroy();
                keysToRemove.Add(pair.Key);
            }

            foreach (var key in keysToRemove)
            {
                poolsByKey.Remove(key);
            }
        }

        private static bool IsAlive(GameObject gameObject)
        {
            return gameObject;
        }

        private static void RegisterTrackedObject(GameObject gameObject, Pool pool)
        {
            if (!IsAlive(gameObject))
            {
                return;
            }

            poolByGameObject[gameObject] = pool;
            poolableCallbacksByGameObject[gameObject] = CachePoolables(gameObject);
        }

        private static void UnregisterTrackedObject(GameObject gameObject)
        {
            if (ReferenceEquals(gameObject, null))
            {
                return;
            }

            poolByGameObject.Remove(gameObject);
            poolableCallbacksByGameObject.Remove(gameObject);
        }

        private static IPoolable[] CachePoolables(GameObject gameObject)
        {
            var behaviours = gameObject.GetComponents<MonoBehaviour>();
            var callbacks = new List<IPoolable>(behaviours.Length);

            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour is IPoolable poolable)
                {
                    callbacks.Add(poolable);
                }
            }

            return callbacks.ToArray();
        }

        private static void InvokeOnAllocate(GameObject gameObject)
        {
            if (!poolableCallbacksByGameObject.TryGetValue(gameObject, out var callbacks))
            {
                return;
            }

            for (int i = 0; i < callbacks.Length; i++)
            {
                callbacks[i]?.OnAllocate();
            }
        }

        private static void InvokeOnDeallocate(GameObject gameObject)
        {
            if (!poolableCallbacksByGameObject.TryGetValue(gameObject, out var callbacks))
            {
                return;
            }

            for (int i = 0; i < callbacks.Length; i++)
            {
                callbacks[i]?.OnDeallocate();
            }
        }

        private struct PoolKey : IEquatable<PoolKey>
        {
            private readonly int prefabId;
            private readonly int parentId;
            private readonly bool persistent;

            public PoolKey(GameObject prefab, GameObject parent, bool persistent)
            {
                prefabId = prefab ? prefab.GetInstanceID() : 0;
                parentId = parent ? parent.GetInstanceID() : 0;
                this.persistent = persistent;
            }

            public override bool Equals(object obj)
            {
                return obj is PoolKey other && Equals(other);
            }

            public bool Equals(PoolKey other)
            {
                return prefabId == other.prefabId
                    && parentId == other.parentId
                    && persistent == other.persistent;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + prefabId;
                    hash = (hash * 31) + parentId;
                    hash = (hash * 31) + (persistent ? 1 : 0);
                    return hash;
                }
            }
        }

        private class Pool
        {
            private readonly int initialSize;
            private readonly GameObject pooledPrefab;
            private readonly Queue<GameObject> disabledGameObjects = new Queue<GameObject>();
            private readonly HashSet<GameObject> enabledGameObjects = new HashSet<GameObject>();
            private readonly GameObject parent;
            private readonly bool ownsParent;

            public bool IsPersistent { get; }

            public Pool(GameObject poolParent, GameObject pooledPrefab, int initialSize, bool persistent = false)
            {
                this.pooledPrefab = pooledPrefab;
                this.initialSize = initialSize;
                IsPersistent = persistent;

                if (poolParent)
                {
                    parent = poolParent;
                    ownsParent = false;
                }
                else
                {
                    parent = new GameObject($"{this.pooledPrefab.name}_Pool");
                    ownsParent = true;
                }

                for (int i = 0; i < this.initialSize; i++)
                {
                    var gameObject = InstantiatePooledObject();
                    gameObject.SetActive(false);
                    disabledGameObjects.Enqueue(gameObject);
                }
            }

            public void Deallocate(GameObject gameObject)
            {
                if (!IsAlive(gameObject))
                {
                    UnregisterTrackedObject(gameObject);
                    return;
                }

                if (!enabledGameObjects.Remove(gameObject))
                {
                    return;
                }

                InvokeOnDeallocate(gameObject);
                gameObject.SetActive(false);
                disabledGameObjects.Enqueue(gameObject);
            }

            public GameObject Allocate(Vector3 position, Quaternion rotation)
            {
                GameObject gameObject = null;

                while (disabledGameObjects.Count > 0 && !IsAlive(gameObject))
                {
                    gameObject = disabledGameObjects.Dequeue();
                    if (!IsAlive(gameObject))
                    {
                        UnregisterTrackedObject(gameObject);
                    }
                }

                if (!IsAlive(gameObject))
                {
                    gameObject = ExtendPool();
                }

                gameObject.transform.SetPositionAndRotation(position, rotation);
                gameObject.SetActive(true);
                enabledGameObjects.Add(gameObject);
                InvokeOnAllocate(gameObject);
                return gameObject;
            }

            public void ClearAndDestroy()
            {
                var activeSnapshot = new List<GameObject>(enabledGameObjects);
                enabledGameObjects.Clear();

                for (int i = 0; i < activeSnapshot.Count; i++)
                {
                    DestroyTrackedObject(activeSnapshot[i]);
                }

                while (disabledGameObjects.Count > 0)
                {
                    DestroyTrackedObject(disabledGameObjects.Dequeue());
                }

                if (ownsParent && IsAlive(parent))
                {
                    UnityEngine.Object.Destroy(parent);
                }
            }

            private GameObject ExtendPool()
            {
                return InstantiatePooledObject();
            }

            private GameObject InstantiatePooledObject()
            {
                GameObject gameObject = UnityEngine.Object.Instantiate(pooledPrefab);
                gameObject.transform.SetParent(parent ? parent.transform : null);
                RegisterTrackedObject(gameObject, this);
                return gameObject;
            }

            private static void DestroyTrackedObject(GameObject gameObject)
            {
                if (ReferenceEquals(gameObject, null))
                {
                    return;
                }

                UnregisterTrackedObject(gameObject);

                if (gameObject)
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
            }
        }

        public interface IPoolable
        {
            void OnAllocate();
            void OnDeallocate();
        }
    }
}
