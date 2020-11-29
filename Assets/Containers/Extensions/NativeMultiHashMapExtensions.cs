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

        public static bool TryMoveNextUniqueKey<TKey, TValue>(this NativeMultiHashMap<TKey, TValue> map, ref UniqueKeysIterator<TKey> it)
         where TKey : unmanaged, System.IEquatable<TKey>
         where TValue : struct
        {
            var data = map.m_MultiHashMapData.m_Buffer;
            var bucketArray = (int*)data->buckets;
            var bucketNext = (int*)data->next;
            while (it.CurrentIndex != -1)
            {
                it.CurrentIndex = bucketNext[it.CurrentIndex];
                if (it.CurrentIndex == -1) break;
                var key = UnsafeUtility.ReadArrayElement<TKey>(data->keys, it.CurrentIndex);
                if (!key.Equals(it.Key))
                {
                    var firstIndexForReadedKey = bucketArray[it.BucketIndex];
                    while (firstIndexForReadedKey != it.CurrentIndex)
                    {
                        var previousKey = UnsafeUtility.ReadArrayElement<TKey>(data->keys, firstIndexForReadedKey);
                        if (key.Equals(previousKey)) break;
                        firstIndexForReadedKey = bucketNext[firstIndexForReadedKey];
                    }
                    it.Key = key;
                    return true;
                }
            }
            //first key in bucket always unique
            while (it.BucketIndex < data->bucketCapacityMask)
            {
                it.BucketIndex++;
                it.CurrentIndex = bucketArray[it.BucketIndex];
                if (it.CurrentIndex != -1)
                {
                    it.Key = UnsafeUtility.ReadArrayElement<TKey>(data->keys, it.CurrentIndex);
                    return true;
                }
            }
            return false;
        }
    }

    public struct UniqueKeysIterator<TKey> where TKey : unmanaged
    {
        public int BucketIndex;
        public int CurrentIndex;
        public TKey Key;
    }
}