# Review target

Review `TenantCache.cs` for concrete correctness, isolation, and reliability defects. Do not modify it.

Contract:

- cache entries and tenant-scoped clearing must never affect another tenant;
- expiration uses UTC instants;
- concurrent misses for one key share one load;
- failed or cancelled loads are not retained;
- caller cancellation propagates to the caller.
