using Kingdee.BOS.Core.Bill.PlugIn;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Orm.DataEntity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;

namespace Ben.HL.FIN.FA.Business.PlugIn
{
    [Kingdee.BOS.Util.HotUpdate]
    [Description("Ben-资产卡片审核推送")]
    public class AssetToMes : AbstractBillPlugIn
    {
        private AssetCardPushService _pushService = new AssetCardPushService();

        public override void AfterDoOperation(AfterDoOperationEventArgs e)
        {
            //PushToMES();
            if (e.Operation.Operation.Equals("Audit", StringComparison.OrdinalIgnoreCase))
            {
                PushToMES();
            }
            base.AfterDoOperation(e);
        }

        private void PushToMES()
        {
            try
            {
                // 声明 message 变量
                string message;
                bool success = PushToMES(this.Model.DataObject, out message);

                if (success)
                {
                    this.View.ShowMessage(message);
                }
                else
                {
                    this.View.ShowMessage(message, MessageBoxType.Error);
                    //this.View.ShowErrMessage(message);
                }
            }
            catch (Exception ex)
            {
                this.View.ShowMessage($"MES推送失败：{ex.Message}", MessageBoxType.Error);
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
                string responseData = _pushService.SendHttpRequest(jsonData);

                System.Diagnostics.Debug.WriteLine($"响应内容：{responseData}");

                // 处理返回结果
                string errorMsg;
                if (_pushService.HandleResponse(responseData, out errorMsg))
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

                    if (billData["Number"] != null)
                    {
                        asset.assetCode = billData["Number"].ToString();//AssetNO
                    }

                    // 设备编码 - 明细字段
                    if (billData["FdevCode"] != null && billData["FdevCode"].ToString().Trim() != "")//F_BHD_Text_xzcbm
                    {
                        asset.devCode = billData["FdevCode"].ToString();
                    }
                    else // 出厂编码 - 主表字段
                    if (billData["F_BHD_Text_xzcbm"] != null && billData["F_BHD_Text_xzcbm"].ToString().Trim() != "")
                    {
                        asset.devCode = billData["F_BHD_Text_xzcbm"].ToString();
                    }
                    else
                    {
                        asset.devCode = billData["Number"].ToString();
                    }


                    // 规格型号 - 明细字段
                    if (entry["Specification"] != null)
                    {
                        asset.specificationAndModel = entry["Specification"].ToString();
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

                    // 设备名称 - 主表字段
                    if (billData["Name"] != null)
                    {
                        asset.devName = billData["Name"].ToString();

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
                    if (billData["F_BHD_Text_xzcbm"] != null && billData["F_BHD_Text_xzcbm"].ToString().Trim() != "")
                    {
                        asset.factoryLeaveCode = billData["F_BHD_Text_xzcbm"].ToString();
                    }
                    else
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
                            if (positionObj != null && positionObj["F_BHD_Costcenter"] != null)
                            {
                                //asset.storageLocation = positionObj["Name"].ToString();
                                asset.departmentName = positionObj["F_BHD_Costcenter"].ToString();
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
    }
}