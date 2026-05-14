using System;

namespace tiktok_Omni.Services
{
    public class CheckpointDetectedException : Exception
    {
        public CheckpointDetectedException()
            : base("Checkpoint detected.")
        {
        }

        public CheckpointDetectedException(string message)
            : base(message)
        {
        }

        public CheckpointDetectedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
