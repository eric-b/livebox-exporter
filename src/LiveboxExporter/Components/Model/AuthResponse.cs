namespace LiveboxExporter.Components.Model
{
    public class AuthResponse
    {
        public int Status { get; set; }
        public AuthResponseData Data { get; set; }
        public AuthResponseError[]? Errors { get; set; }

        public class AuthResponseData
        {
            public string? ContextID { get; set; }
            public string? Groups { get; set; }
        }

        public class AuthResponseError
        {
            public string Description { get; set; }
            public int Waittime { get; set; }
        }
    }
}
