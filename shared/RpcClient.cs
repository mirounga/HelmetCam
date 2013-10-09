using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HelmetCam
{
    public class RpcClient
    {
        private HttpClient endPoint;

        public RpcClient(Uri endpointUri)
        {
            endPoint = new HttpClient()
            {
                BaseAddress = endpointUri
            };
        }

        public async Task<T> CameraMethod<T>(string methodName, params object[] args)
        {
            JArray resultArray = await CameraMethod(methodName, args);

            return resultArray[0].ToObject<T>();
        }

        private async Task<JArray> CameraMethod(string methodName, params object[] args)
        {
            JObject requestObject = new JObject();

            requestObject["method"] = methodName;

            JArray methodParams = new JArray();

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    methodParams.Add(JValue.FromObject(args[i]));
                }
            }

            requestObject["params"] = methodParams;

            requestObject["id"] = 1;
            requestObject["version"] = "1.0";

            string requestText = requestObject.ToString();

            HttpResponseMessage response = await endPoint.PostAsync("camera", new StringContent(requestText));

            JObject responseRoot = null;

            Stream responseStream = await response.Content.ReadAsStreamAsync();

            using (responseStream)
            {
                using (StreamReader responseReader = new StreamReader(responseStream))
                {
                    using (JsonReader jsonReader = new JsonTextReader(responseReader))
                    {
                        responseRoot = JObject.Load(jsonReader);
                    }
                }
            }

            JArray responseResult = (JArray)responseRoot["result"];

            return responseResult;
        }
    }
}
