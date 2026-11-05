using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;

namespace MuseumPuzzle.Server.Platform
{
    /// <summary>
    /// <see cref="ISecrets"/> on Unity's Secret Manager, through the Cloud Code game API client
    /// (<c>SecretManager.GetSecret</c>, docs.unity.com, "Secret Manager: Cloud Code modules"). Cloud Code keeps a
    /// secret's value for up to five minutes, so a value the owner changes takes that long to arrive. A secret not
    /// set (404) is nothing; any other failure is <see cref="SecretsUnavailableException"/>.
    /// </summary>
    public sealed class UgsSecrets : ISecrets
    {
        private readonly ISecretClient _secrets;
        private readonly IExecutionContext _context;

        public UgsSecrets(ISecretClient secrets, IExecutionContext context)
        {
            _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<string?> ReadAsync(SecretName name, CancellationToken cancellation)
        {
            string key = SecretNames.KeyOf(name);
            try
            {
                Secret secret = await _secrets.GetSecret(_context, key).ConfigureAwait(false);
                return string.IsNullOrEmpty(secret?.Value) ? null : secret.Value;
            }
            catch (ApiException failure) when (failure.Type == ApiExceptionType.Http && failure.Response?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (ApiException failure)
            {
                throw new SecretsUnavailableException($"The Secret Manager did not give {key}: {failure.Type} {(int?)failure.Response?.StatusCode}.", failure);
            }
        }
    }
}
