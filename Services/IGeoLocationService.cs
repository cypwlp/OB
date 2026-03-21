using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OB.Services
{
    public interface IGeoLocationService
    {
        Task<string> GetCountryCodeAsync(CancellationToken cancellationToken = default);
    }
}
