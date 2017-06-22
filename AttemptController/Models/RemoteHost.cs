using System;

namespace AttemptController.Models
{
    public class RemoteHost
    {
        public Uri Uri { get; set; }

        public override string ToString()
        {
            return Uri.ToString();
        }
    }

    public class TestRemoveHost : RemoteHost
    {
        public string KeyPrefix { get; set; }

        public new string ToString()
        {
            return KeyPrefix + base.ToString();
        }
    }
}
