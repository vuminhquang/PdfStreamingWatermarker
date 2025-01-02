PDF Watermarking Azure Function
=============================

Service Limits and Capacity
--------------------------
1. Concurrent Processing:
   - Max concurrent HTTP requests: 100 (set by SemaphoreSlim in code)
   - Max outstanding requests: 200
   - Worker processes per instance: 8 (set by FUNCTIONS_WORKER_PROCESS_COUNT)
   - Max scale-out instances: 10 (set by WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT)
   - Total theoretical max: 100 × 10 = 1000 concurrent requests

2. Queue Processing:
   - Queue batch size: 16 messages (set in host.json)
   - Queue polling interval: 2 seconds (set in host.json)
   - Max retry attempts: 5
   - New batch threshold: 8
   - Requests exceeding capacity are automatically queued
   - Queue Message Lifecycle:
     * Message arrives → Picked up within 2 seconds (polling interval)
     * Processing starts → Message invisible for 30 seconds
     * If processing succeeds → Message deleted
     * If processing fails → Message visible again after 30 seconds
     * After 5 failures → Message moves to poison queue

3. Instance Scaling:
   - Scale Out Triggers:
     * HTTP: When concurrent requests > 100, overflow to queue
     * Queue: QueueTrigger automatically creates new instances
       - Each instance processes up to 16 messages (batchSize)
       - Up to maximum 10 instances (WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT)
   - Scale out managed by Azure platform via QueueTrigger
   - Each new instance provides:
     * 100 more concurrent HTTP capacity
     * 16 more queue message batch size
     * 8 more worker processes
   - Traffic Pattern Examples:
     * 10 requests     → Instance 1 only
     * 150 requests    → Instance 1 processing + Queue + Scale as needed
     * 500 requests    → Multiple instances + Queue
     * 1000+ requests  → Up to 10 instances + Queue overflow

4. Scale Down Behavior:
   - Additional instances removed after ~5 minutes of inactivity
   - Base instance maintained by warmup endpoint
   - No cold starts with warmup enabled
   - Trade-off: Small cost for better availability

Load Handling
------------
1. Direct Processing (Default Path):
   - First 100 concurrent requests per instance processed immediately
   - No queue involvement
   - Response: HTTP 200 with streamed PDF
   - Most efficient for normal load

2. Queue Fallback (When System Busy):
   - Only triggered when direct processing unavailable
   - Happens when concurrent requests > 100 per instance
   - Response: HTTP 202 Accepted with status URL
   - Output: Check "watermarked_[filename]"
   - Provides overflow protection during peak loads

Best Practices
-------------
1. Large Files:
   - Use queue processing for files > 10MB
   - Consider breaking very large batches

2. High Volume:
   - Monitor queue length
   - Watch for timeout errors
   - Use warmup path for cold starts

3. Cost Optimization:
   - Linux consumption plan
   - Automatic scale to zero when idle
   - Queue-based processing for peaks

Monitoring
----------
1. Key Metrics:
   - Concurrent requests
   - Queue length
   - Processing time
   - Memory usage
   - Instance count

2. Logging:
   - Application Insights enabled
   - Debug logging for PDF processing
   - Error logging for failures

Configuration
------------
See host.json and deployment script for detailed settings.
Contact system administrator for adjusting limits. 