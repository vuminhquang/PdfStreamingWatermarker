PDF Watermarking Azure Function
=============================

Service Limits and Capacity
--------------------------
1. Concurrent Processing:
   - Max concurrent HTTP requests: 100 per instance (set in host.json)
   - Max outstanding requests: 200 per instance (request queue before 429)
   - Worker processes per instance: 4 (set by FUNCTIONS_WORKER_PROCESS_COUNT)
   - Max scale-out instances: 20 (set by WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT)
   - Total theoretical max: 100 × 20 = 2000 concurrent requests

2. Instance Scaling:
   - Scale Out Triggers:
     * Based on sustained concurrent executions (70% utilization for 5-10 minutes)
     * Each instance handles up to 100 concurrent requests
     * Up to maximum 20 instances
   - Scale Down:
     * Occurs when usage drops below 50% for 10-20 minutes
     * Base instance maintained by warmup endpoint
   - Traffic Pattern Examples:
     * 50 requests      → Instance 1 only
     * 150 requests     → Instance 1 (100) + Queue (50) until scale-out
     * 500 requests     → Multiple instances
     * 2000+ requests   → Up to 20 instances, then 429 responses

Load Handling
------------
1. Direct Processing:
   - All requests processed synchronously
   - First 100 concurrent requests per instance processed immediately
   - Next 200 requests queued internally
   - Beyond that: HTTP 429 (Too Many Requests)
   - Response: HTTP 200 with streamed PDF
   - Timeout: 10 minutes max per request

Best Practices
-------------
1. Large Files:
   - Consider client timeout settings
   - Monitor processing times
   - Implement retry logic in client

2. High Volume:
   - Monitor concurrent requests
   - Watch for 429 responses
   - Use exponential backoff in client
   - Consider time-of-day patterns

3. Cost Optimization:
   - Linux consumption plan
   - Automatic scale to zero when idle
   - Warmup endpoint prevents cold starts

Monitoring
----------
1. Key Metrics:
   - Concurrent requests
   - Request duration
   - Memory usage
   - Instance count
   - HTTP 429 responses

2. Logging:
   - Application Insights enabled
   - Debug logging for PDF processing
   - Error logging for failures

Configuration
------------
See host.json and deployment script for detailed settings.
Contact system administrator for adjusting limits.

Note: All requests are now processed synchronously. The system will queue requests
internally when busy and automatically scale out based on sustained load. 