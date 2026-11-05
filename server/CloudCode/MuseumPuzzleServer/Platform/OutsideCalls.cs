using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MuseumPuzzle.Server.Platform
{
    /// <summary>The time the module reads: real time in Cloud Code, a hand-set time in the tests.</summary>
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    /// <summary>An answer from Google or Apple: its status and its body, read in full.</summary>
    public sealed class OutsideAnswer
    {
        public OutsideAnswer(HttpStatusCode status, string body)
        {
            Status = status;
            Body = body ?? string.Empty;
        }

        public HttpStatusCode Status { get; }

        public string Body { get; }

        public bool IsSuccess => (int)Status >= 200 && (int)Status <= 299;

        /// <summary>A refusal that trying again later may get past: too many requests, or a fault of the other side.</summary>
        public bool IsPassing => Status == HttpStatusCode.TooManyRequests || (int)Status >= 500;
    }

    /// <summary>Google or Apple did not answer: no network, a refused connection, or no answer within the module's patience. The message says whom; the cause is the transport's exception.</summary>
    public sealed class OutsideUnreachableException : Exception
    {
        public OutsideUnreachableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>Google or Apple answered with something the module cannot read: a layout its documentation does not give. The message says whose answer; the cause is the reader's exception.</summary>
    public sealed class StoreAnswerUnreadableException : Exception
    {
        public StoreAnswerUnreadableException(string message)
            : base(message)
        {
        }

        public StoreAnswerUnreadableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>What is wrong with a store's credentials. A closed set.</summary>
    public enum CredentialProblem
    {
        /// <summary>The owner has not entered the secret (yet).</summary>
        Missing,

        /// <summary>The secret holds something that is not the key it should be.</summary>
        Malformed,

        /// <summary>The store refuses the key: revoked, of another account, or without the permissions it needs.</summary>
        Refused,
    }

    /// <summary>A store's credentials cannot be used; the message says which and why, and never holds the secret.</summary>
    public sealed class StoreCredentialsException : Exception
    {
        public StoreCredentialsException(CredentialProblem problem, string message)
            : base(message)
        {
            Problem = problem;
        }

        public StoreCredentialsException(CredentialProblem problem, string message, Exception innerException)
            : base(message, innerException)
        {
            Problem = problem;
        }

        public CredentialProblem Problem { get; }
    }

    /// <summary>
    /// The module's calls to Google and Apple. One client for the module's life (a client per call would spend a
    /// connection each), with a patience of five seconds for each answer (docs/research/server-v1.md §4.2): a Cloud
    /// Code call has fifteen in all.
    /// </summary>
    public static class OutsideCalls
    {
        public static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

        public static HttpClient NewClient() => new HttpClient { Timeout = Patience };

        /// <summary>Sends the request and reads the whole answer, whatever its status.</summary>
        /// <param name="whom">Who is asked, for the message of a failure ("Google Play's purchases").</param>
        /// <exception cref="OutsideUnreachableException">No answer came.</exception>
        public static async Task<OutsideAnswer> SendAsync(HttpClient http, HttpRequestMessage request, string whom, CancellationToken cancellation)
        {
            try
            {
                using HttpResponseMessage response = await http.SendAsync(request, cancellation).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
                return new OutsideAnswer(response.StatusCode, body);
            }
            catch (HttpRequestException unreachable)
            {
                throw new OutsideUnreachableException($"{whom} could not be reached.", unreachable);
            }
            catch (TaskCanceledException timedOut) when (!cancellation.IsCancellationRequested)
            {
                throw new OutsideUnreachableException($"{whom} did not answer within {Patience.TotalSeconds:0} seconds.", timedOut);
            }
        }

        /// <summary>The text with base64url (RFC 4648 §5, no padding): how a JSON Web Token writes its parts.</summary>
        public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static string Base64Url(string text) => Base64Url(Encoding.UTF8.GetBytes(text));

        /// <summary>The bytes of a base64url text.</summary>
        /// <exception cref="FormatException">The text is not base64url.</exception>
        public static byte[] FromBase64Url(string text)
        {
            string standard = text.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(standard.PadRight(standard.Length + ((4 - (standard.Length % 4)) % 4), '='));
        }
    }

    /// <summary>How the module reads the stores' JSON answers.</summary>
    public static class StoreJson
    {
        /// <exception cref="StoreAnswerUnreadableException">The body is not JSON of this layout.</exception>
        public static T Read<T>(string body, string whose)
            where T : class
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<T>(body) ?? throw new StoreAnswerUnreadableException($"{whose} is empty.");
            }
            catch (System.Text.Json.JsonException unreadable)
            {
                throw new StoreAnswerUnreadableException($"{whose} is not the JSON its documentation gives.", unreadable);
            }
        }
    }

    /// <summary>
    /// The one access token the module keeps for a store between calls (Google's for 55 minutes, Apple's for 25):
    /// the only thing it keeps at all, so its memory never grows. The token is kept with a fingerprint of the
    /// credentials it was made from, so a secret the owner changed is used at once. A Cloud Code worker may not see
    /// the next call, so this only saves a request now and then; nothing depends on it.
    /// </summary>
    public sealed class TokenSlot
    {
        private readonly object _gate = new object();
        private string? _fingerprint;
        private string? _token;
        private DateTimeOffset _until;

        /// <summary>The token kept for these credentials, if it is good until after <paramref name="now"/>.</summary>
        public string? Held(string fingerprint, DateTimeOffset now)
        {
            lock (_gate)
            {
                return _token != null && _fingerprint == fingerprint && now < _until ? _token : null;
            }
        }

        public void Keep(string fingerprint, string token, DateTimeOffset until)
        {
            lock (_gate)
            {
                _fingerprint = fingerprint;
                _token = token;
                _until = until;
            }
        }

        /// <summary>Forgets the token: the store refused it.</summary>
        public void Drop()
        {
            lock (_gate)
            {
                _fingerprint = null;
                _token = null;
            }
        }
    }
}
