using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;

namespace SIPSorcery.Net
{
    public class PatchedDtlsServerProtocol : DtlsServerProtocol
    {
        protected override void ProcessCertificateVerify(ServerHandshakeState state, byte[] body, TlsHandshakeHash handshakeHash)
        {
            
        }
    }
}
