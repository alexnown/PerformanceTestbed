using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Collections
{

    [JobProducerType(typeof(JobNativeMultiHashMapVisitKeyValue.JobNativeMultiHashMapVisitKeyValueProducer<,,>))]
    public interface IJobNativeMultiHashMapVisitKeyValue<TKey, TValue>
        where TKey : struct, IEquatable<TKey>
        where TValue : struct
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void ExecuteNext(TKey key, TValue value);
    }

    public static class JobNativeMultiHashMapVisitKeyValue
    {
        internal struct JobNativeMultiHashMapVisitKeyValueProducer<TJob, TKey, TValue>
            where TJob : struct, IJobNativeMultiHashMapVisitKeyValue<TKey, TValue>
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
                    s_JobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobNativeMultiHashMapVisitKeyValueProducer<TJob, TKey, TValue>), typeof(TJob), JobType.ParallelFor, (ExecuteJobFunction)Execute);
                }

                return s_JobReflectionData;
            }

            internal delegate void ExecuteJobFunction(ref JobNativeMultiHashMapVisitKeyValueProducer<TJob, TKey, TValue> producer, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            /// <summary>
            ///
            /// </summary>
            /// <param name="producer"></param>
            /// <param name="additionalPtr"></param>
            /// <param name="bufferRangePatchData"></param>
            /// <param name="ranges"></param>
            /// <param name="jobIndex"></param>
            public static unsafe void Execute(ref JobNativeMultiHashMapVisitKeyValueProducer<TJob, TKey, TValue> producer, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
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
                            var value = UnsafeUtility.ReadArrayElement<TValue>(values, entryIndex);

                            producer.JobData.ExecuteNext(key, value);

                            entryIndex = nextPtrs[entryIndex];
                        }
                    }
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="TJob"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="jobData"></param>
        /// <param name="hashMap"></param>
        /// <param name="minIndicesPerJobCount"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static unsafe JobHandle Schedule<TJob, TKey, TValue>(this TJob jobData, NativeMultiHashMap<TKey, TValue> hashMap, int minIndicesPerJobCount, JobHandle dependsOn = new JobHandle())
            where TJob : struct, IJobNativeMultiHashMapVisitKeyValue<TKey, TValue>
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
        {
            var jobProducer = new JobNativeMultiHashMapVisitKeyValueProducer<TJob, TKey, TValue>
            {
                HashMap = hashMap,
                JobData = jobData
            };

            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref jobProducer)
                , JobNativeMultiHashMapVisitKeyValueProducer<TJob, TKey, TValue>.Initialize()
                , dependsOn
                , ScheduleMode.Batched
            );

            return JobsUtility.ScheduleParallelFor(ref scheduleParams, hashMap.GetUnsafeBucketData().bucketCapacityMask + 1, minIndicesPerJobCount);
        }
    }
}
