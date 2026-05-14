using System;

namespace tiktok_Omni.Services
{
    public class CaptchaDetectedException : Exception
    {
        public CaptchaDetectedException()
            : base("TikTok challenge or CAPTCHA was detected. Solve it manually then resume the warm-up.")
        {
        }

        public CaptchaDetectedException(string message)
            : base(message)
        {
        }

        public CaptchaDetectedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
