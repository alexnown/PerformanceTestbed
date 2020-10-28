# PerformanceTestbed
Contains performance comparisons for different use cases

### Compare ways to get unique keys from NativeMultiHashMap (100k unique keys, 2M total values)
* Default GetUniqueKeyArray extension ~150ms
* Save keys with StoreKeysToHashSet extension ~100ms
* Get keys array, sort and count unique in bursted job ~35ms
* Save keys with StoreKeysToHashSet extension in bursted job ~12ms
