namespace tiktok_Omni.Services.Jobs
{
    public enum OmniJobStatus
    {
        Pending = 0,
        Running = 1,
        RetryPending = 2,
        Completed = 3,
        Failed = 4,
        Cancelled = 5,
        Processing = 6
    }
}
