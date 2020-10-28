using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Collections
{
    public unsafe static class NativeMultiHashMapExtensions
    {
        public static void StoreKeysToHashSet<TKey, TValue>(this NativeMultiHashMap<TKey, TValue> map, NativeHashSet<TKey> keysStore)
             where TKey : unmanaged, System.IEquatable<TKey>
             where TValue : struct
        {
            var data = map.m_MultiHashMapData.m_Buffer;
            var bucketArray = (int*)data->buckets;
            var bucketNext = (int*)data->next;

            for (int i = 0; i <= data->bucketCapacityMask; ++i)
            {
                int bucket = bucketArray[i];

                while (bucket != -1)
                {
                    var key = UnsafeUtility.ReadArrayElement<TKey>(data->keys, bucket);
                    keysStore.Add(key);
                    bucket = bucketNext[bucket];
                }
            }
        }
        public static NativeArray<TKey> FixedGetKeyArray<TKey, TValue>(this NativeMultiHashMap<TKey, TValue> map, Allocator allocator)
             where TKey : struct, System.IEquatable<TKey>
             where TValue : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(map.m_Safety);
#endif
            var result = new NativeArray<TKey>(map.m_MultiHashMapData.Count(), allocator, NativeArrayOptions.UninitializedMemory);
            FixedGetKeyArray(map.m_MultiHashMapData.m_Buffer, result);
            return result;
        }
        internal static void FixedGetKeyArray<TKey>(UnsafeHashMapData* data, NativeArray<TKey> result)
                where TKey : struct
        {
            var bucketArray = (int*)data->buckets;
            var bucketNext = (int*)data->next;
            int count = 0;
            int max = result.Length;
            for (int i = 0; i <= data->bucketCapacityMask && count < max; ++i)
            {
                int bucket = bucketArray[i];
                while (bucket != -1)
                {
                    result[count++] = UnsafeUtility.ReadArrayElement<TKey>(data->keys, bucket);
                    bucket = bucketNext[bucket];
                }
            }
        }
    }
}