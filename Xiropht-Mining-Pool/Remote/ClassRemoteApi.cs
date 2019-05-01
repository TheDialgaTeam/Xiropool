using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Xiropht_Connector_All.Setting;
using Xiropht_Mining_Pool.Api;
using Xiropht_Mining_Pool.Setting;

namespace Xiropht_Mining_Pool.Remote
{
    public class ClassRemoteApi
    {

        public static async Task<string> GetBlockInformation(string blockHeight)
        {
            string request = "get_coin_block_per_id=" + blockHeight;
            string result = await ProceedHttpRequest("http://" + MiningPoolSetting.MiningPoolRemoteNodeHost + ":" + MiningPoolSetting.MiningPoolRemoteNodePort + "/", request);
            if (result != ClassApiEnumeration.PacketNotExist)
            {
                return result;
            }
            return null;
        }

        public static async Task<string> GetNetworkInformation()
        {
            string request = "get_coin_network_full_stats";
            string result = await ProceedHttpRequest("http://" + MiningPoolSetting.MiningPoolRemoteNodeHost + ":" + MiningPoolSetting.MiningPoolRemoteNodePort + "/", request);
            if (result != ClassApiEnumeration.PacketNotExist)
            {
                return result;
            }
            return null;
        }

        private static async Task<string> ProceedHttpRequest(string url, string requestString)
        {
            string result = string.Empty;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url + requestString);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.ServicePoint.Expect100Continue = false;
            request.KeepAlive = false;
            request.Timeout = 5000;
            request.UserAgent = ClassConnectorSetting.CoinName + " Mining Pool Tool - " + Assembly.GetExecutingAssembly().GetName().Version + "R";
            string responseContent = string.Empty;
            using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                result = await reader.ReadToEndAsync();
            }

            return result;
        }
    }
}
