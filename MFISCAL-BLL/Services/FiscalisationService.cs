using Microsoft.Extensions.Logging;
using MFISCAL_INF.Environments;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using MFISCAL_INF.Models;

namespace MFISCAL_BLL.Services
{
    public interface IFiscalizationService
    {
        Task<string> SendEvidentirajERacunAsync(XDocument invoiceXml, string invoiceId, CancellationToken ct = default);
    }

    public class FiscalizationService : IFiscalizationService
    {
        private readonly ILogger<FiscalizationService> _logger;
        private readonly LocalEnvironmentValues _envValues;
        private readonly X509Certificate2 _signingCertificate;
        private readonly X509Certificate2? _clientCertificate;

        public FiscalizationService(ILogger<FiscalizationService> logger, ILocalEnvironment env)
        {
            _logger = logger;
            _envValues = env.Values;

            var certPath = _envValues.FiscalSigningCertPath;
            var certPassword = _envValues.FiscalSigningCertPassword;
            var thumbprint = _envValues.FiscalSigningCertThumbprint;

            if (!string.IsNullOrEmpty(certPath))
            {
                _signingCertificate = new X509Certificate2(
                    certPath,
                    certPassword,
                    X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
            }
            else if (!string.IsNullOrEmpty(thumbprint))
            {
                using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
                if (certs.Count == 0) throw new InvalidOperationException($"Signing certificate with thumbprint {thumbprint} not found.");
                _signingCertificate = certs[0];
            }
            else
            {
                throw new InvalidOperationException("No signing certificate configured. Provide Path+Password or Thumbprint.");
            }

            var clientCertPath = _envValues.FiscalClientCertPath;
            var clientCertPassword = _envValues.FiscalClientCertPassword;
            if (!string.IsNullOrEmpty(clientCertPath))
            {
                _clientCertificate = new X509Certificate2(
                    clientCertPath,
                    clientCertPassword,
                    X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
            }
        }

        public async Task<string> SendEvidentirajERacunAsync(XDocument invoiceXml, string invoiceId, CancellationToken ct = default)
        {
            var requestXmlDoc = BuildFiscalizationRequestXml(invoiceXml, invoiceId);
            SignXmlDocument(requestXmlDoc, _signingCertificate);
            var signedRequestString = requestXmlDoc.OuterXml;
            await SaveAuditAsync($"request_{invoiceId}.xml", signedRequestString);
            var soapEnvelope = BuildSoapEnvelope(signedRequestString);
            var eduEndpoint = _envValues.FiscalEduEndpoint;
            if (string.IsNullOrEmpty(eduEndpoint)) throw new InvalidOperationException("Fiscal EDU endpoint not configured.");
            using var handler = new HttpClientHandler();
            if (_clientCertificate != null)
                handler.ClientCertificates.Add(_clientCertificate);
            using var httpClient = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "application/soap+xml");
            HttpResponseMessage resp;
            try
            {
                resp = await httpClient.PostAsync(eduEndpoint, content, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling fiscal EDU endpoint");
                throw;
            }
            var respBody = await resp.Content.ReadAsStringAsync(ct);
            await SaveAuditAsync($"response_{invoiceId}.xml", respBody);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("EDU endpoint returned {StatusCode}: {Body}", resp.StatusCode, respBody);
                throw new InvalidOperationException($"EDU endpoint returned {(int)resp.StatusCode}: {resp.ReasonPhrase}");
            }
            try
            {
                var respXmlDoc = new XmlDocument { PreserveWhitespace = true };
                respXmlDoc.LoadXml(respBody);
                var verified = ValidateXmlSignature(respXmlDoc);
                if (!verified)
                {
                    _logger.LogWarning("Response signature verification failed. Response may be tampered or EDU cert not trusted.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate response signature (non-fatal).");
            }
            var jir = ExtractJirFromResponse(respBody);
            _logger.LogInformation("Received JIR {Jir} for invoice {InvoiceId}", jir, invoiceId);
            return respBody;
        }

        private XmlDocument BuildFiscalizationRequestXml(XDocument invoiceXml, string invoiceId)
        {
            var wrapper = new XDocument(
                new XElement("EvidentirajERacunZahtjev",
                    new XElement("ZahtjevID", Guid.NewGuid().ToString()),
                    new XElement("DatumVrijeme", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")),
                    new XElement("Racun", invoiceXml.Root)
                )
            );
            var xmlDoc = new XmlDocument { PreserveWhitespace = true };
            using var sr = new StringReader(wrapper.ToString(SaveOptions.DisableFormatting));
            xmlDoc.Load(sr);
            return xmlDoc;
        }

        private void SignXmlDocument(XmlDocument xmlDoc, X509Certificate2 signingCert)
        {
            var signedXml = new SignedXml(xmlDoc)
            {
                SigningKey = signingCert.GetRSAPrivateKey()
            };
            var reference = new Reference()
            {
                Uri = ""
            };
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            reference.AddTransform(new XmlDsigExcC14NTransform());
            reference.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
            signedXml.AddReference(reference);
            if (signedXml.SignedInfo != null)
            {
                signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
            }
            var keyInfo = new KeyInfo();
            var x509Data = new KeyInfoX509Data(signingCert);
            x509Data.AddSubjectName(signingCert.Subject);
            keyInfo.AddClause(x509Data);
            signedXml.KeyInfo = keyInfo;
            signedXml.ComputeSignature();
            var xmlDigitalSignature = signedXml.GetXml();
            if (xmlDoc.DocumentElement != null)
            {
                xmlDoc.DocumentElement.AppendChild(xmlDoc.ImportNode(xmlDigitalSignature, true));
            }
        }

        private string BuildSoapEnvelope(string signedRequestXml)
        {
            var soap = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:urn=""urn:hr:porezna:fiskalizacija"">
  <soapenv:Header/>
  <soapenv:Body>
    {signedRequestXml}
  </soapenv:Body>
</soapenv:Envelope>";
            return soap;
        }

        private bool ValidateXmlSignature(XmlDocument xmlDoc)
        {
            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
            var signatureNode = xmlDoc.SelectSingleNode("//ds:Signature", namespaceManager) as XmlElement;
            if (signatureNode == null)
            {
                _logger.LogWarning("No Signature element found in response.");
                return false;
            }
            var signedXml = new SignedXml(xmlDoc);
            signedXml.LoadXml(signatureNode);
            X509Certificate2? signerCert = null;
            var keyInfoEnum = signedXml.KeyInfo.GetEnumerator();
            while (keyInfoEnum.MoveNext())
            {
                if (keyInfoEnum.Current is KeyInfoX509Data kd)
                {
                    if (kd.Certificates != null)
                    {
                        foreach (var obj in kd.Certificates)
                        {
                            if (obj is X509Certificate2 cert)
                            {
                                signerCert = cert;
                                break;
                            }
                            if (obj is System.Security.Cryptography.X509Certificates.X509Certificate rawCert)
                            {
                                signerCert = new X509Certificate2(rawCert);
                                break;
                            }
                        }
                    }
                }
            }
            if (signerCert != null)
            {
                return signedXml.CheckSignature(signerCert, true);
            }
            else
            {
                _logger.LogWarning("No signer certificate found in response KeyInfo; cannot validate signature without Tax Admin cert.");
                return false;
            }
        }

        private string? ExtractJirFromResponse(string responseXml)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(responseXml);
                var jirNode = doc.SelectSingleNode("//Jir") ?? doc.SelectSingleNode("//jir") ?? doc.SelectSingleNode("//*[local-name()='Jir']");
                return jirNode != null ? jirNode.InnerText : null;
            }
            catch
            {
                return null;
            }
        }

        private Task SaveAuditAsync(string fileName, string content)
        {
            var auditFolder = _envValues.FiscalAuditFolder ?? Path.Combine(AppContext.BaseDirectory, "fiscal_audit");
            Directory.CreateDirectory(auditFolder);
            var path = Path.Combine(auditFolder, fileName);
            return File.WriteAllTextAsync(path, content);
        }
    }
}
