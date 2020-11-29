using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.PerformanceTesting;

public class LargeAmountOfUniqueKeys_CountKeysPerformanceComparison : AGetUniqueIntKeysPerformanceComparison<int>
{
    public override int ValuesTotalCount => 2000000;
    public override int UniqueKeysCount => 100000;
}

public class SmallAmountOfUniqueKeys_CountKeysPerformanceComparison : AGetUniqueIntKeysPerformanceComparison<int>
{
    public override int ValuesTotalCount => 2000000;
    public override int UniqueKeysCount => 50;
}

public abstract class AGetUniqueIntKeysPerformanceComparison<TValue> : AGetUniqueKeysPerformanceComparison<int, TValue> where TValue : struct
{
    public abstract int ValuesTotalCount { get; }
    public virtual int Capacity => ValuesTotalCount;
    public override NativeMultiHashMap<int, TValue> InitializeMap()
    {
        var map = new NativeMultiHashMap<int, TValue>(Capacity, Allocator.TempJob);
        int valuesForKey = ValuesTotalCount / UniqueKeysCount;
        for (int i = 0; i < UniqueKeysCount; i++)
        {
            for (int j = 0; j < valuesForKey; j++)
            {
                map.Add(i, default);
            }
        }
        var count = valuesForKey * UniqueKeysCount;
        while (count++ < ValuesTotalCount) map.Add(0, default);
        return map;
    }
}

public abstract class AGetUniqueKeysPerformanceComparison<TKey, TValue>
    where TKey : unmanaged, System.IEquatable<TKey>, System.IComparable<TKey>
    where TValue : struct
{
    public abstract int UniqueKeysCount { get; }
    public abstract NativeMultiHashMap<TKey, TValue> InitializeMap();

    [Test, Performance]
    public void DefaultGetUniqueKeys()
    {
        NativeMultiHashMap<TKey, TValue> map = default;
        Measure.Method(() =>
        {
            var uniqueKeys = map.GetUniqueKeyArray(Allocator.TempJob);
            Assert.AreEqual(UniqueKeysCount, uniqueKeys.Item2);
            uniqueKeys.Item1.Dispose();
        })
            .SetUp(() => map = InitializeMap())
            .CleanUp(() => map.Dispose())
            .Run();
    }

    [Test, Performance]
    public void UniqueKeysIterator()
    {
        NativeMultiHashMap<TKey, TValue> map = default;
        Measure.Method(() =>
        {
            var it = new UniqueKeysIterator<TKey> { BucketIndex = -1, CurrentIndex = -1 };
            int counter = 0;
            while (map.TryMoveNextUniqueKey(ref it)) counter++;
            Assert.AreEqual(UniqueKeysCount, counter);
        })
            .SetUp(() => map = InitializeMap())
            .CleanUp(() => map.Dispose())
            .Run();
    }

    [Test, Performance]
    public void StoreToHashSet()
    {
        NativeMultiHashMap<TKey, TValue> map = default;
        NativeHashSet<TKey> keys = default;
        Measure.Method(() =>
        {
            map.StoreKeysToHashSet(keys);
            Assert.AreEqual(UniqueKeysCount, keys.Count());
        })
            .SetUp(() =>
            {
                map = InitializeMap();
                keys = new NativeHashSet<TKey>(UniqueKeysCount, Allocator.TempJob);
            })
            .CleanUp(() =>
            {
                map.Dispose();
                keys.Dispose();
            })
            .Run();
    }
    [Test, Performance]
    public void GetUniqueKeysInBurstedJob()
    {
        NativeMultiHashMap<TKey, TValue> map = default;
        NativeArray<int> resultsArray = default;
        Measure.Method(() =>
        {
            new CountUniqueKeysJob
            {
                Map = map,
                Result = resultsArray
            }.Run();
            Assert.AreEqual(UniqueKeysCount, resultsArray[0]);
        })
            .SetUp(() =>
            {
                map = InitializeMap();
                resultsArray = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            })
            .CleanUp(() =>
            {
                map.Dispose();
                resultsArray.Dispose();
            })
            .Run();
    }

    [Test, Performance]
    public void StoreToHashSetInBurstedJob()
    {
        NativeMultiHashMap<TKey, TValue> map = default;
        NativeHashSet<TKey> keys = default;
        NativeArray<int> resultsArray = default;
        Measure.Method(() =>
        {
            new StoreKeysToHashSetJob
            {
                Map = map,
                Keys = keys,
                Result = resultsArray
            }.Run();
            Assert.AreEqual(UniqueKeysCount, resultsArray[0]);
        })
            .SetUp(() =>
            {
                map = InitializeMap();
                keys = new NativeHashSet<TKey>(UniqueKeysCount, Allocator.TempJob);
                resultsArray = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            })
            .CleanUp(() =>
            {
                map.Dispose();
                keys.Dispose();
                resultsArray.Dispose();
            })
            .Run();
    }

    [Test, Performance]
    public void StoreToHashSetInBurstedParallelJob()
    {
        NativeMultiHashMap<TKey, TValue> map = default;
        NativeHashSet<TKey> keys = default;
        Measure.Method(() =>
        {
            new StoreKeysToHashSetParallelJob
            {
                Keys = keys.AsParallelWriter()
            }.Schedule(map, 1024).Complete();
            Assert.AreEqual(UniqueKeysCount, keys.Count());
        })
            .SetUp(() =>
            {
                map = InitializeMap();
                keys = new NativeHashSet<TKey>(UniqueKeysCount, Allocator.TempJob);
            })
            .CleanUp(() =>
            {
                map.Dispose();
                keys.Dispose();
            })
            .Run();
    }

    [Test, Performance]
    public void UniqueKeysIteratorBurstedJob()
    {
        NativeMultiHashMap<TKey, TValue> map = default;
        NativeArray<int> resultsArray = default;
        Measure.Method(() =>
        {
            new IterateOverUniqueKeysJob
            {
                Map = map,
                Result = resultsArray
            }.Run();
            Assert.AreEqual(UniqueKeysCount, resultsArray[0]);
        })
            .SetUp(() =>
            {
                map = InitializeMap();
                resultsArray = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            })
            .CleanUp(() =>
            {
                map.Dispose();
                resultsArray.Dispose();
            })
            .Run();
    }

    [Unity.Burst.BurstCompile]
    struct CountUniqueKeysJob : IJob
    {
        [ReadOnly]
        public NativeMultiHashMap<TKey, TValue> Map;
        [WriteOnly]
        public NativeArray<int> Result;
        public void Execute()
        {
            var keys = Map.GetKeyArray(Allocator.Temp);
            keys.Sort();
            Result[0] = keys.Unique();
        }
    }
    [Unity.Burst.BurstCompile]
    struct StoreKeysToHashSetJob : IJob
    {
        [ReadOnly]
        public NativeMultiHashMap<TKey, TValue> Map;
        public NativeHashSet<TKey> Keys;
        [WriteOnly]
        public NativeArray<int> Result;
        public void Execute()
        {
            Map.StoreKeysToHashSet(Keys);
            Result[0] = Keys.Count();
        }
    }
    [Unity.Burst.BurstCompile]
    struct StoreKeysToHashSetParallelJob : IJobNativeMultiHashMapVisitKey<TKey>
    {
        public NativeHashSet<TKey>.ParallelWriter Keys;
        public void ExecuteNext(TKey key) => Keys.Add(key);
    }

    [Unity.Burst.BurstCompile]
    struct IterateOverUniqueKeysJob : IJob
    {
        [ReadOnly]
        public NativeMultiHashMap<TKey, TValue> Map;
        [WriteOnly]
        public NativeArray<int> Result;
        public void Execute()
        {
            var it = new UniqueKeysIterator<TKey> { BucketIndex = -1, CurrentIndex = -1 };
            int counter = 0;
            while (Map.TryMoveNextUniqueKey(ref it)) counter++;
            Result[0] = counter;
        }
    }
}
