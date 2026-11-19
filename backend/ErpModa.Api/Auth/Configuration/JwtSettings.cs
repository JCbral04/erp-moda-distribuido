namespace ErpModa.Api.Auth.Configuration
{
    public sealed class JwtSettings
    {
        public const string SectionName = "Jwt";

        /// <summary>Clave de firma (≥32 caracteres). Nunca versionar.</summary>
        public string Key { get; set; } = string.Empty;

        public string Issuer { get; set; } = "ErpModa";
        public string Audience { get; set; } = "ErpModa.Client";
        public int DuracionMinutos { get; set; } = 60;
    }
}
