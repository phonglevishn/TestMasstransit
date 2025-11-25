using System;

namespace Example.Contracts
{
    public class DirectRequest
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DirectResponse
    {
        public int Id { get; set; }
        public DateTime RequestTimestamp { get; set; }
        public DateTime ResponseTimestamp { get; set; }
    }
    public class DirectRequestB
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DirectResponseB
    {
        public int Id { get; set; }
        public DateTime RequestTimestamp { get; set; }
        public DateTime ResponseTimestamp { get; set; }
    }
}