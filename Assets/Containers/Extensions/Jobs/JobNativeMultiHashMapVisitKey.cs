using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Collections
{

    [JobProducerType(typeof(JobNativeMultiHashMapVisitKey.JobNativeMultiHashMapVisitKeyProducer<,,>))]
    public interface IJobNativeMultiHashMapVisitKey<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        void ExecuteNext(TKey key);
    }

    public static class JobNativeMultiHashMapVisitKey
    {
        internal struct JobNativeMultiHashMapVisitKeyProducer<TJob, TKey, TValue>
            where TJob : struct, IJobNativeMultiHashMapVisitKey<TKey>
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
        {
            [ReadOnly] public NativeMultiHashMap<TKey, TValue> HashMap;
            internal TJob JobData;

            static IntPtr s_JobReflectionData;

            internal static IntPtr Initialize()
            {
                if (s_JobReflectionData == IntPtr.Zero)
                {
                    s_JobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobNativeMultiHashMapVisitKeyProducer<TJob, TKey, TValue>), typeof(TJob), JobType.ParallelFor, (ExecuteJobFunction)Execute);
                }

                return s_JobReflectionData;
            }

            internal delegate void ExecuteJobFunction(ref JobNativeMultiHashMapVisitKeyProducer<TJob, TKey, TValue> producer, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public static unsafe void Execute(ref JobNativeMultiHashMapVisitKeyProducer<TJob, TKey, TValue> producer, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                while (true)
                {
                    int begin;
                    int end;

                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                    {
                        return;
                    }
                    var bucketData = producer.HashMap.GetUnsafeBucketData();
                    var buckets = (int*)bucketData.buckets;
                    var nextPtrs = (int*)bucketData.next;
                    var keys = bucketData.keys;
                    var values = bucketData.values;

                    for (int i = begin; i < end; i++)
                    {
                        int entryIndex = buckets[i];

                        while (entryIndex != -1)
                        {
                            var key = UnsafeUtility.ReadArrayElement<TKey>(keys, entryIndex);
                            producer.JobData.ExecuteNext(key);
                            entryIndex = nextPtrs[entryIndex];
                        }
                    }
                }
            }
        }

        public static unsafe JobHandle Schedule<TJob, TKey, TValue>(this TJob jobData, NativeMultiHashMap<TKey, TValue> hashMap, int minIndicesPerJobCount, JobHandle dependsOn = new JobHandle())
            where TJob : struct, IJobNativeMultiHashMapVisitKey<TKey>
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
        {
            var jobProducer = new JobNativeMultiHashMapVisitKeyProducer<TJob, TKey, TValue>
            {
                HashMap = hashMap,
                JobData = jobData
            };

            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref jobProducer)
                , JobNativeMultiHashMapVisitKeyProducer<TJob, TKey, TValue>.Initialize()
                , dependsOn
                , ScheduleMode.Batched
            );

            return JobsUtility.ScheduleParallelFor(ref scheduleParams, hashMap.GetUnsafeBucketData().bucketCapacityMask + 1, minIndicesPerJobCount);
        }
    }
}
