using Kingdee.BOS.Orm.DataEntity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Ben.HL.FIN.FA.Business.PlugIn
{
    /// <summary>
    /// 资产卡片推送公共业务类
    /// </summary>
    public class AssetCardPushService
    {
        private readonly string _apiUrl = "http://192.168.1.6:80/iMark/v1/DBEquipmentArchivesInfo/createOrModifyList";

        /// <summary>
        /// 获取资产卡片数据（支持明细多行）
        /// </summary>
        public List<AssetCardModel> GetAssetCardData(DynamicObject billData)
        {
            var assetList = new List<AssetCardModel>();

            // 获取当前单据的数据对象
            DynamicObjectCollection financeData = billData["Finance"] as DynamicObjectCollection;
            DynamicObjectCollection allocationData = billData["Allocation"] as DynamicObjectCollection;

            // 获取卡片明细单据体
            DynamicObjectCollection entries = null;
            if (billData["CardDetail"] != null)
            {
                entries = billData["CardDetail"] as DynamicObjectCollection;
            }

            if (entries != null && entries.Count > 0)
            {
                // 遍历明细行
                foreach (DynamicObject entry in entries)
                {
                    var asset = new AssetCardModel();

                    // 资产编码 - 明细字段
                    if (entry["AssetNO"] != null)
                    {
                        asset.assetCode = entry["AssetNO"].ToString();
                    }

                    // 单位 - 主表字段
                    if (billData["UnitID"] != null)
                    {
                        DynamicObject unitObj = billData["UnitID"] as DynamicObject;
                        if (unitObj != null && unitObj["Name"] != null)
                        {
                            asset.unit = unitObj["Name"].ToString();
                        }
                    }

                    // 数量 - 主表字段
                    if (billData["Quantity"] != null)
                    {
                        decimal? quantityDecimal = billData["Quantity"] as decimal?;
                        if (quantityDecimal.HasValue)
                        {
                            asset.quantity = (int)quantityDecimal.Value;
                        }
                    }

                    // 供应商 - 明细字段
                    if (entry["SupplierID"] != null)
                    {
                        DynamicObject supplierObj = entry["SupplierID"] as DynamicObject;
                        if (supplierObj != null && supplierObj["Name"] != null)
                        {
                            asset.supplier = supplierObj["Name"].ToString();
                        }
                    }

                    // 出厂编码 - 主表字段
                    if (billData["Number"] != null)
                    {
                        asset.factoryLeaveCode = billData["Number"].ToString();
                    }

                    // 出厂日期/购入日期 - 财务信息
                    if (financeData != null && financeData.Count > 0)
                    {
                        DynamicObject firstFinance = financeData[0];
                        if (firstFinance["AcctDate"] != null)
                        {
                            DateTime factoryDate = firstFinance["AcctDate"] as DateTime? ?? DateTime.MinValue;
                            if (factoryDate != DateTime.MinValue)
                            {
                                asset.factoryLeaveDate = factoryDate.ToString("yyyy-MM-dd");
                                asset.purchaseDate = factoryDate.ToString("yyyy-MM-dd");
                            }
                        }
                    }

                    // 存放地点/部门名称 - 明细字段
                    if (entry["PositionID"] != null)
                    {
                        DynamicObject positionObj = entry["PositionID"] as DynamicObject;
                        if (positionObj != null && positionObj["Name"] != null)
                        {
                            asset.storageLocation = positionObj["Name"].ToString();
                            //asset.departmentName = positionObj["Name"].ToString();
                        }
                    }

                    if (allocationData != null && allocationData.Count > 0)
                    {
                        DynamicObject firstAllocation = allocationData[0];
                        if (firstAllocation["AllocUseDeptID"] != null)
                        {
                            DynamicObject positionObj = firstAllocation["AllocUseDeptID"] as DynamicObject;
                            if (positionObj != null && positionObj["Name"] != null)
                            {
                                //asset.storageLocation = positionObj["Name"].ToString();
                                asset.departmentName = positionObj["Name"].ToString();
                            }
                        }
                    }

                    // 设备类型 - 主表字段
                    if (billData["AssetTypeID"] != null)
                    {
                        DynamicObject typeObj = billData["AssetTypeID"] as DynamicObject;
                        if (typeObj != null && typeObj["Name"] != null)
                        {
                            asset.equipmentType = typeObj["Name"].ToString();
                        }
                    }

                    // 负责人 - 明细字段
                    if (entry["KEEPERID"] != null)
                    {
                        DynamicObject managerObj = entry["KEEPERID"] as DynamicObject;
                        if (managerObj != null && managerObj["Name"] != null)
                        {
                            asset.manager = managerObj["Name"].ToString();
                        }
                    }
                    assetList.Add(asset);
                }
            }
            return assetList;
        }

        /// <summary>
        /// 从查询结果获取资产卡片数据（用于列表批量推送）
        /// </summary>
        public List<AssetCardModel> GetAssetCardDataFromQuery(DynamicObjectCollection dataCollection)
        {
            var assetList = new List<AssetCardModel>();

            if (dataCollection == null || dataCollection.Count == 0)
                return assetList;

            foreach (DynamicObject item in dataCollection)
            {
                var asset = new AssetCardModel();

                // 资产编码
                if (item["FAssetNO"] != null)
                {
                    asset.assetCode = item["FAssetNO"].ToString();
                }

                // 单位
                if (item["FUnitID_FName"] != null)
                {
                    asset.unit = item["FUnitID_FName"].ToString();
                }

                // 数量
                if (item["FQuantity"] != null)
                {
                    decimal? quantityDecimal = item["FQuantity"] as decimal?;
                    if (quantityDecimal.HasValue)
                    {
                        asset.quantity = (int)quantityDecimal.Value;
                    }
                }

                // 供应商
                if (item["FSupplierID_FName"] != null)
                {
                    asset.supplier = item["FSupplierID_FName"].ToString();
                }

                // 出厂编码
                if (item["FNumber"] != null)
                {
                    asset.factoryLeaveCode = item["FNumber"].ToString();
                }

                // 购入日期
                if (item["FAcctDate"] != null)
                {
                    DateTime acctDate = item["FAcctDate"] as DateTime? ?? DateTime.MinValue;
                    if (acctDate != DateTime.MinValue)
                    {
                        asset.factoryLeaveDate = acctDate.ToString("yyyy-MM-dd");
                        asset.purchaseDate = acctDate.ToString("yyyy-MM-dd");
                    }
                }

                // 存放地点
                if (item["FPositionID_FName"] != null)
                {
                    asset.storageLocation = item["FPositionID_FName"].ToString();
                }

                // 使用部门
                if (item["FAllocUseDeptID_FName"] != null)
                {
                    asset.departmentName = item["FAllocUseDeptID_FName"].ToString();
                }

                // 设备类型
                if (item["FAssetTypeID_FName"] != null)
                {
                    asset.equipmentType = item["FAssetTypeID_FName"].ToString();
                }

                // 负责人
                if (item["FKEEPERID_FName"] != null)
                {
                    asset.manager = item["FKEEPERID_FName"].ToString();
                }

                assetList.Add(asset);
            }

            return assetList;
        }

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
        /// 完整的推送方法
        /// </summary>
        /// <param name="billData">单据数据对象</param>
        /// <param name="resultMessage">返回结果消息</param>
        /// <returns>是否推送成功</returns>
        public bool PushToMES(DynamicObject billData, out string resultMessage)
        {
            resultMessage = string.Empty;

            try
            {
                // 获取当前资产卡片数据
                var assetData = GetAssetCardData(billData);

                if (assetData == null || assetData.Count == 0)
                {
                    resultMessage = "没有需要推送的资产数据";
                    return false;
                }

                // 【关键修正】MES接口期望直接接收数组，不需要包装成对象
                string jsonData = JsonConvert.SerializeObject(assetData, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DateFormatString = "yyyy-MM-dd"
                });

                System.Diagnostics.Debug.WriteLine($"请求JSON：{jsonData}");

                // 发送HTTP请求
                string responseData = SendHttpRequest(jsonData);

                System.Diagnostics.Debug.WriteLine($"响应内容：{responseData}");

                // 处理返回结果
                string errorMsg;
                if (HandleResponse(responseData, out errorMsg))
                {
                    resultMessage = $"MES推送成功！共处理{assetData.Count}条记录";
                    return true;
                }
                else
                {
                    resultMessage = errorMsg;
                    return false;
                }
            }
            catch (WebException ex)
            {
                // 获取详细的错误响应
                string errorResponse = "";
                if (ex.Response != null)
                {
                    using (StreamReader reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        errorResponse = reader.ReadToEnd();
                    }
                }
                resultMessage = $"网络请求失败：{ex.Message}，响应：{errorResponse}";
                return false;
            }
            catch (HttpRequestException ex)
            {
                resultMessage = $"HTTP请求异常：{ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                resultMessage = $"MES推送失败：{ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 异步推送方法（带回调）
        /// </summary>
        public async void PushToMESAsync(DynamicObject billData, Action<bool, string> callback)
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                string message;
                bool success = PushToMES(billData, out message);
                callback?.Invoke(success, message);
            });
        }
    }

    /// <summary>
    /// 资产卡片数据模型
    /// </summary>
    public class AssetCardModel
    {
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