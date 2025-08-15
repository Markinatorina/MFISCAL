using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MFISCAL_INF.Models
{
    public class LocalEnvironmentValues
    {
        public required string JwtIssuerSigningKey { get; init; }
        public required string JwtIssuerName { get; init; }
        public required string JwtIssuerAudience { get; init; }
        public required string AdminUsername { get; init; }
        public required string AdminPassword { get; init; }
        public required string PostgresBaseDbUser { get; init; }
        public required string PostgresBaseDbPassword { get; init; }
        public required string PostgresBaseDbHost { get; init; }
        public required int PostgresBaseDbPort { get; init; }
        public required string PostgresBaseDbDbName { get; init; }
        public required string PostgresBaseDbSslMode { get; init; }
        public string? FiscalSigningCertPath { get; init; }
        public string? FiscalSigningCertPassword { get; init; }
        public string? FiscalSigningCertThumbprint { get; init; }
        public string? FiscalClientCertPath { get; init; }
        public string? FiscalClientCertPassword { get; init; }
        public string? FiscalEduEndpoint { get; init; }
        public string? FiscalAuditFolder { get; init; }
    }
}
