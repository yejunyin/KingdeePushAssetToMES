using Kingdee.BOS.Core.Bill.PlugIn;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Orm.DataEntity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ben.HL.FIN.FA.Business.PlugIn
{
    [Kingdee.BOS.Util.HotUpdate]
    [Description("Ben-资产卡片审核推送")]
    public class buttonclass : AbstractBillPlugIn
    {
        private readonly string _apiUrl = "http://127.0.0.1:90/iMark/v1/DBEquipmentArchivesInfo/createOrModifyList";

        /// <summary>
        /// 审核后推送数据到MES
        /// </summary>
        public override void AfterDoOperation(AfterDoOperationEventArgs e)
        {
            Task.Run(() => PushToMES());
            if (e.Operation.Operation.Equals("Audit", StringComparison.OrdinalIgnoreCase))
            {
                // 异步推送，避免阻塞主线程
                Task.Run(() => PushToMES());
            }
            base.AfterDoOperation(e);
        }

        /// <summary>
        /// 推送资产卡片数据到MES
        /// </summary>
        private void PushToMES()
        {
            try
            {
                // 获取当前资产卡片数据
                var assetData = GetAssetCardData();

                if (assetData == null || assetData.Count == 0)
                {
                    this.View.ShowMessage("没有需要推送的资产数据", MessageBoxType.Notice);
                    return;
                }

                // 序列化JSON
                string jsonData = JsonConvert.SerializeObject(assetData, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DateFormatString = "yyyy-MM-dd"
                });

                // 发送HTTP请求
                string result = SendHttpRequest(jsonData);

                // 处理返回结果
                HandleMESResponse(result);

                this.View.ShowMessage("资产卡片推送MES成功");
            }
            catch (WebException ex)
            {
                this.View.ShowMessage($"网络请求失败：{ex.Message}", MessageBoxType.Error);
            }
            catch (HttpRequestException ex)
            {
                this.View.ShowMessage($"HTTP请求异常：{ex.Message}", MessageBoxType.Error);
            }
            catch (Exception ex)
            {
                this.View.ShowMessage($"MES推送失败：{ex.Message}", MessageBoxType.Error);
            }
        }

        /// <summary>
        /// 获取资产卡片数据（支持明细多行）
        /// </summary>
        private List<AssetCardModel> GetAssetCardData()
        {
            var assetList = new List<AssetCardModel>();

            // 获取当前单据的数据对象
            DynamicObject billData = this.Model.DataObject;

            // 常见资产卡片单据体字段名：FEntity、FDetailEntity、FAssetCardEntry等
            // 请根据实际字段名修改，常见的几种方式：
            DynamicObjectCollection entries = null;

            // 方式1：尝试获取单据体（最常见的是FEntity）
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

                    // 资产编码 - 主表字段
                    if (billData["AssetNO"] != null)
                    {
                        asset.assetCode = billData["AssetNO"].ToString();
                    }

                    // 从明细行获取数据
                    // 功率 - 根据实际明细字段名修改
                    if (entry["Power"] != null)
                    {
                        decimal power;
                        if (decimal.TryParse(entry["Power"].ToString(), out power))
                        {
                            asset.power = power;
                        }
                    }

                    // 单位 - 根据实际明细字段名修改
                    if (entry["Unit"] != null)
                    {
                        DynamicObject unitObj = entry["Unit"] as DynamicObject;
                        if (unitObj != null && unitObj["FName"] != null)
                        {
                            asset.unit = unitObj["FName"].ToString();
                        }
                    }

                    // 数量 - 根据实际明细字段名修改
                    if (entry["DetailQuantity"] != null)
                    {
                        int quantity;
                        if (int.TryParse(entry["DetailQuantity"].ToString(), out quantity))
                        {
                            asset.quantity = quantity;
                        }
                    }

                    // 供应商 - 根据实际明细字段名修改
                    if (entry["Supplier"] != null)
                    {
                        DynamicObject supplierObj = entry["Supplier"] as DynamicObject;
                        if (supplierObj != null && supplierObj["Name"] != null)
                        {
                            asset.supplier = supplierObj["Name"].ToString();
                        }
                    }

                    // 出厂编码 - 根据实际明细字段名修改
                    if (entry["FactoryCode"] != null)
                    {
                        asset.factoryLeaveCode = entry["FactoryCode"].ToString();
                    }

                    // 出厂日期 - 根据实际明细字段名修改
                    if (entry["FactoryDate"] != null)
                    {
                        DateTime factoryDate = entry["FactoryDate"] as DateTime? ?? DateTime.MinValue;
                        if (factoryDate != DateTime.MinValue)
                        {
                            asset.factoryLeaveDate = factoryDate.ToString("yyyy-MM-dd");
                        }
                    }

                    // 购入日期 - 根据实际明细字段名修改
                    if (entry["PurchaseDate"] != null)
                    {
                        DateTime purchaseDate = entry["PurchaseDate"] as DateTime? ?? DateTime.MinValue;
                        if (purchaseDate != DateTime.MinValue)
                        {
                            asset.purchaseDate = purchaseDate.ToString("yyyy-MM-dd");
                        }
                    }

                    // 存放地点 - 根据实际明细字段名修改
                    if (entry["StorageLocation"] != null)
                    {
                        asset.storageLocation = entry["StorageLocation"].ToString();
                    }

                    // 设备类型 - 根据实际明细字段名修改
                    if (entry["EquipmentType"] != null)
                    {
                        DynamicObject typeObj = entry["EquipmentType"] as DynamicObject;
                        if (typeObj != null && typeObj["Name"] != null)
                        {
                            asset.equipmentType = typeObj["Name"].ToString();
                        }
                    }

                    // 部门名称 - 根据实际明细字段名修改
                    if (entry["Department"] != null)
                    {
                        DynamicObject deptObj = entry["Department"] as DynamicObject;
                        if (deptObj != null && deptObj["Name"] != null)
                        {
                            asset.departmentName = deptObj["Name"].ToString();
                        }
                    }

                    // 负责人 - 根据实际明细字段名修改
                    if (entry["Manager"] != null)
                    {
                        DynamicObject managerObj = entry["Manager"] as DynamicObject;
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
        /// 发送HTTP请求到MES接口
        /// </summary>
        private string SendHttpRequest(string jsonData)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "Kingdee-Cloud-Seas");

                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                HttpResponseMessage response = client.PostAsync(_apiUrl, content).Result;

                if (response.IsSuccessStatusCode)
                {
                    return response.Content.ReadAsStringAsync().Result;
                }
                else
                {
                    throw new HttpRequestException($"HTTP请求失败，状态码：{response.StatusCode}");
                }
            }
        }

        /// <summary>
        /// 处理MES接口返回结果
        /// </summary>
        private void HandleMESResponse(string result)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new Exception("MES接口返回空数据");
            }

            // 根据实际返回格式解析
            try
            {
                var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
                if (response != null && response.ContainsKey("code"))
                {
                    string code = response["code"]?.ToString();
                    if (code != "200" && code != "0" && code != "success")
                    {
                        string message = response.ContainsKey("message") ? response["message"].ToString() : "未知错误";
                        throw new Exception($"MES接口返回错误：{message}");
                    }
                }
            }
            catch (JsonException)
            {
                // 如果返回的不是JSON格式，忽略异常，认为成功
            }
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
}