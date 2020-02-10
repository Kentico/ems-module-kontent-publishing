using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using CMS.EventLog;
using CMS.SiteProvider;

using Newtonsoft.Json;
using Polly;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class SyncBase
    {
        private const string ApiRoot = "https://manage.kontent.ai/v2";
        private SyncSettings _settings;

        HttpClient _client = new HttpClient();
               
        public SyncSettings Settings
        {
            get
            {
                return _settings;
            }
        }

        private enum RetryHttpCode
        {
            RequestTimeout = 408,
            TooManyRequests = 429,
            InternalServerError = 500,
            BadGateway = 502,
            ServiceUnavailable = 503,
            GatewayTimeout = 504,
        }

        public const int MAX_RETRIES = 5;

        // Only HTTP status codes are handled with retries, not exceptions.
        private IAsyncPolicy<HttpResponseMessage> _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(result => Enum.IsDefined(typeof(RetryHttpCode), (RetryHttpCode)result.StatusCode))
            .WaitAndRetryAsync(
                MAX_RETRIES,
                retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100)
            );

        private JsonSerializerSettings _serializeSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            //Converters = new List<JsonConverter> { new DecimalObjectConverter() }
        };

        private JsonSerializerSettings _serializeSettingsKeepNull = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            //Converters = new List<JsonConverter> { new DecimalObjectConverter() }
        };


        public SyncBase(SyncSettings settings)
        {
            _settings = settings;
        }

        private HttpRequestMessage CreateMessage(string endpointUrl, HttpMethod method, object payload = null, bool includeNullValues = false)
        {
            var message = new HttpRequestMessage(method, endpointUrl);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.CMApiKey);

            if (payload != null)
            {
                string json = JsonConvert.SerializeObject(
                    payload,
                    Formatting.None,
                    includeNullValues ? _serializeSettingsKeepNull : _serializeSettings
                );
                message.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return message;
        }

        private async Task HandleStatusCode(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                throw new HttpException(
                    (int)response.StatusCode,
                    $"Request to Kentico Kontent failed with status code {response.StatusCode}, operation:\n{response.RequestMessage.Method} {response.RequestMessage.RequestUri}, response content:\n\n{content}"
                );
            }
        }

        private async Task<HttpResponseMessage> SendMessage(Func<HttpRequestMessage> createMessage)
        {
            var attempts = 0;

            var policyResult = await _retryPolicy.ExecuteAndCaptureAsync(
                async () => {
                    attempts++;
                    var message = createMessage();
                    return await _client.SendAsync(message);
                }
            );

            var result = policyResult.FinalHandledResult ?? policyResult.Result;

            if (result == null)
            {
                SyncLog.LogEvent(EventType.ERROR, "KenticoKontentPublishing", "REQUESTFAIL", $"Retry attempts: {attempts}");
            }

            return result;
        }

        protected async Task ExecuteWithoutResponse(string endpoint, HttpMethod method, object payload = null, bool includeNullValues = false)
        {
            var endpointUrl = $"{ApiRoot}/projects/{_settings.ProjectId}{endpoint}";

            using (var response = await SendMessage(() => CreateMessage(endpointUrl, method, payload, includeNullValues)))
            {
                await HandleStatusCode(response);
            }
        }

        protected async Task<T> ExecuteWithResponse<T>(string endpoint, HttpMethod method, object payload = null, bool includeNullValues = false) where T : class
        {
            var endpointUrl = $"{ApiRoot}/projects/{_settings.ProjectId}{endpoint}";

            using (var response = await SendMessage(() => CreateMessage(endpointUrl, method, payload, includeNullValues)))
            {
                // 404 response is valid response for GET requests
                if ((method == HttpMethod.Get) && (response.StatusCode == HttpStatusCode.NotFound))
                {
                    return null;
                }

                await HandleStatusCode(response);

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseData = JsonConvert.DeserializeObject<T>(responseJson);

                return responseData;
            }
        }

        private HttpRequestMessage CreateUploadMessage(string endpointUrl, byte[] data, string contentType)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.CMApiKey);

            message.Content = new ByteArrayContent(data);
            message.Content.Headers.ContentLength = data.Length;
            message.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            return message;
        }

        protected async Task<T> ExecuteUploadWithResponse<T>(string endpoint, byte[] data, string contentType) where T : class
        {
            var endpointUrl = $"{ApiRoot}/projects/{_settings.ProjectId}{endpoint}";

            using (var response = await SendMessage(() => CreateUploadMessage(endpointUrl, data, contentType)))
            {
                await HandleStatusCode(response);

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseData = JsonConvert.DeserializeObject<T>(responseJson);

                return responseData;
            }
        }

        public bool IsSynchronizedSite(int siteId)
        {
            var synchronizedSiteId = SiteInfoProvider.GetSiteID(Settings.Sitename);
            return synchronizedSiteId == siteId;
        }
    }
}
