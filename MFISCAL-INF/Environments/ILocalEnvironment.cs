using MFISCAL_INF.Models;
using System;
using System.Collections.Generic;

namespace MFISCAL_INF.Environments
{
    public interface ILocalEnvironment
    {
        LocalEnvironmentValues Values { get; }
        byte[] GetSigningKeyBytes();
    }
}
