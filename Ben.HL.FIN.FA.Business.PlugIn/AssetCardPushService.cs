using Kingdee.BOS.Orm.DataEntity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Kingdee.BOS.Core.SqlBuilder;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.List.PlugIn;
using Kingdee.BOS;

namespace Ben.HL.FIN.FA.Business.PlugIn
{
    /// <summary>
    /// 资产卡片推送公共业务类
    /// </summary>
    public class AssetCardPushService
    {
        private readonly string _apiUrl = "http://192.168.1.6:80/iMark/v1/DBEquipmentArchivesInfo/createOrModifyList";

        public Context Context { get; private set; }
        
        /// <summary>
        /// 发送HTTP请求到MES接口
        /// </summary>
        public string SendHttpRequest(string jsonData)
        {
            HttpWebRequest request = null;
            try
            {
                // 创建请求
                request = (HttpWebRequest)WebRequest.Create(_apiUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Timeout = 30000; // 30秒超时

                // 添加 Authorization 头
                string authInfo = Convert.ToBase64String(Encoding.UTF8.GetBytes("bg:bg957768"));
                request.Headers.Add("Authorization", "Basic " + authInfo);

                // 写入请求体
                byte[] byteData = Encoding.UTF8.GetBytes(jsonData);
                request.ContentLength = byteData.Length;

                using (Stream requestBody = request.GetRequestStream())
                {
                    requestBody.Write(byteData, 0, byteData.Length);
                }

                // 获取响应
                using (WebResponse response = request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader streamReader = new StreamReader(responseStream, Encoding.UTF8))
                {
                    return streamReader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                // 处理HTTP错误响应
                if (ex.Response != null)
                {
                    using (StreamReader reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        string errorResponse = reader.ReadToEnd();
                        throw new Exception($"HTTP错误：{ex.Message}，响应：{errorResponse}");
                    }
                }
                throw;
            }
            finally
            {
                request?.Abort();
            }
        }

        /// <summary>
        /// 处理MES接口响应
        /// </summary>
        /// <returns>是否成功，失败时返回错误信息</returns>
        public bool HandleResponse(string responseData, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                // 【关键修正】先打印响应内容用于调试
                System.Diagnostics.Debug.WriteLine($"MES响应内容：{responseData}");

                // 方式1：使用 JObject 动态解析，更加灵活
                var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(responseData);

                // 检查是否有 code 字段
                if (jsonObj["code"] != null)
                {
                    int code = jsonObj["code"].Value<int>();
                    if (code != 0)
                    {
                        string msg = jsonObj["msg"]?.Value<string>() ?? "未知错误";
                        errorMessage = $"MES接口调用失败：{msg}";
                        return false;
                    }
                }

                // 检查是否有 success 字段
                if (jsonObj["success"] != null)
                {
                    bool success = jsonObj["success"].Value<bool>();
                    if (!success)
                    {
                        string msg = jsonObj["msg"]?.Value<string>() ?? "操作失败";
                        errorMessage = $"MES接口调用失败：{msg}";
                        return false;
                    }
                }

                // 检查是否有 data 字段中的错误
                if (jsonObj["data"] != null)
                {
                    var data = jsonObj["data"];
                    if (data["error"] != null && data["error"].HasValues)
                    {
                        var errors = data["error"].Values<string>();
                        errorMessage = $"业务数据校验失败：\n{string.Join("\n", errors)}";
                        return false;
                    }
                }

                return true;
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                errorMessage = $"JSON解析失败：{ex.Message}，原始响应：{responseData}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 异步推送方法（带回调）
        /// </summary>
        //public async void PushToMESAsync(DynamicObject billData, Action<bool, string> callback)
        //{
        //    await System.Threading.Tasks.Task.Run(() =>
        //    {
        //        string message;
        //        bool success = PushToMES(billData, out message);
        //        callback?.Invoke(success, message);
        //    });
        //}

        /// <summary>
        /// 根据部门ID获取成本中心
        /// </summary>
        public string GetCostCenterByDeptId(string deptId)
        {
            try
            {
                if (string.IsNullOrEmpty(deptId))
                {
                    return null;
                }

                // 使用 QueryBuilderParemeter 查询部门基础资料
                QueryBuilderParemeter parameter = new QueryBuilderParemeter();
                parameter.FormId = "BD_Department";

                // 选择需要的字段
                parameter.SelectItems = SelectorItemInfo.CreateItems(
                    "FDEPTID",
                    "F_BHD_CostCenter"
                );

                // 设置过滤条件：按部门ID查询
                parameter.FilterClauseWihtKey = $"FDEPTID = '{deptId}'";

                // 执行查询
                DynamicObjectCollection result = QueryServiceHelper.GetDynamicObjectCollection(
                                    this.Context,
                                    parameter
                                );
                if (result != null && result.Count > 0)
                {
                    var deptData = result[0];
                    var costCenter = deptData["F_BHD_CostCenter"];

                    if (costCenter != null)
                    {
                        // 如果成本中心是基础资料，取编码
                        if (costCenter is DynamicObject)
                        {
                            var costCenterObj = costCenter as DynamicObject;
                            return costCenterObj["Number"]?.ToString();
                        }
                        else
                        {
                            return costCenter.ToString();
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                // 记录日志，但不影响主流程
                System.Diagnostics.Debug.WriteLine($"获取成本中心失败：{ex.Message}");
                return null;
            }
        }
    }

        /// <summary>
        /// 资产卡片数据模型
        /// </summary>
        public class AssetCardModel
    {
        /// <summary>
        /// 设备编码
        /// </summary>
        [JsonProperty("devCode")]
        public string devCode { get; set; }

        /// <summary>
        /// 设备名称
        /// </summary>
        [JsonProperty("devName")]
        public string devName { get; set; }

        /// <summary>
        /// 设备型号
        /// </summary>
        [JsonProperty("specificationAndModel")]
        public string specificationAndModel { get; set; }

        /// <summary>
        /// 资产编码
        /// </summary>
        [JsonProperty("assetCode")]
        public string assetCode { get; set; }

        /// <summary>
        /// 功率
        /// </summary>
        [JsonProperty("power")]
        public decimal? power { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        [JsonProperty("unit")]
        public string unit { get; set; }

        /// <summary>
        /// 数量
        /// </summary>
        [JsonProperty("quantity")]
        public int? quantity { get; set; }

        /// <summary>
        /// 供应商
        /// </summary>
        [JsonProperty("supplier")]
        public string supplier { get; set; }

        /// <summary>
        /// 出厂编码
        /// </summary>
        [JsonProperty("factoryLeaveCode")]
        public string factoryLeaveCode { get; set; }

        /// <summary>
        /// 出厂日期
        /// </summary>
        [JsonProperty("factoryLeaveDate")]
        public string factoryLeaveDate { get; set; }

        /// <summary>
        /// 购入日期
        /// </summary>
        [JsonProperty("purchaseDate")]
        public string purchaseDate { get; set; }

        /// <summary>
        /// 存放地点
        /// </summary>
        [JsonProperty("storageLocation")]
        public string storageLocation { get; set; }

        /// <summary>
        /// 设备类型
        /// </summary>
        [JsonProperty("equipmentType")]
        public string equipmentType { get; set; }

        /// <summary>
        /// 部门名称
        /// </summary>
        [JsonProperty("departmentName")]
        public string departmentName { get; set; }

        /// <summary>
        /// 负责人
        /// </summary>
        [JsonProperty("manager")]
        public string manager { get; set; }
    }

    /// <summary>
    /// API响应实体类
    /// </summary>
    public class ApiResponse
    {
        [JsonProperty("data")]
        public object data { get; set; }

        [JsonProperty("result")]
        public object result { get; set; }

        [JsonProperty("code")]
        public int code { get; set; }

        [JsonProperty("msg")]
        public string msg { get; set; }

        [JsonProperty("success")]
        public bool success { get; set; }
    }

    /// <summary>
    /// 响应数据模型
    /// </summary>
    public class ResponseData
    {
        [JsonProperty("success")]
        public List<string> success { get; set; }

        [JsonProperty("error")]
        public List<string> error { get; set; }
    }
}