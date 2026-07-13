using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.List.PlugIn;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Core.SqlBuilder;
using Kingdee.BOS.ServiceHelper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.Metadata;

namespace Ben.HL.FIN.FA.Business.PlugIn
{
    [Kingdee.BOS.Util.HotUpdate]
    [Description("Ben-资产卡片列表审核推送")]
    public class Listbuttonclass : AbstractListPlugIn
    {
        private AssetCardPushService _pushService = new AssetCardPushService();

        public override void AfterDoOperation(AfterDoOperationEventArgs e)
        {
            if (e.Operation != null && e.Operation.Operation == "Audit")
            {
                PushAfterAudit();
            }
            base.AfterDoOperation(e);
        }

        private void PushAfterAudit()
        {
            try
            {
                List<AssetCardModel> assetDataList = GetSelectedCardData();

                if (assetDataList == null || assetDataList.Count == 0)
                {
                    this.View.ShowMessage("没有获取到需要推送的资产卡片数据！");
                    return;
                }

                //this.View.ShowMessage($"开始推送审核通过的 {assetDataList.Count} 张资产卡片到MES...");
                ProcessBatchPushWithModel(assetDataList);
            }
            catch (Exception ex)
            {
                this.View.ShowMessage($"审核后推送失败：{ex.Message}", MessageBoxType.Error);
            }
        }

        private List<AssetCardModel> GetSelectedCardData()
        {
            List<AssetCardModel> result = new List<AssetCardModel>();
            List<long> billIds = new List<long>();

            try
            {
                ListSelectedRowCollection selectedRows = this.ListView.SelectedRowsInfo;

                if (selectedRows == null || selectedRows.Count == 0)
                {
                    ListSelectedRow currentRow = this.ListView.CurrentSelectedRowInfo;
                    if (currentRow != null)
                    {
                        selectedRows = new ListSelectedRowCollection();
                        selectedRows.Add(currentRow);
                    }
                }

                if (selectedRows == null || selectedRows.Count == 0)
                {
                    this.View.ShowMessage("请先选择要推送的资产卡片！", MessageBoxType.Error);
                    return result;
                }

                foreach (ListSelectedRow row in selectedRows)
                {
                    if (row.PrimaryKeyValue != null)
                    {
                        long id = Convert.ToInt64(row.PrimaryKeyValue);
                        billIds.Add(id);
                    }
                }

                if (billIds.Count == 0)
                {
                    this.View.ShowMessage("未获取到有效的卡片ID", MessageBoxType.Error);
                    return result;
                }

                result = BatchLoadCardData(billIds);
            }
            catch (Exception ex)
            {
                this.View.ShowErrMessage($"获取选中卡片数据失败：{ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 批量加载卡片数据并转换为AssetCardModel
        /// </summary>
        private List<AssetCardModel> BatchLoadCardData(List<long> billIds)
        {
            if (billIds == null || billIds.Count == 0)
                return new List<AssetCardModel>();

            try
            {
                QueryBuilderParemeter parameter = new QueryBuilderParemeter();
                parameter.FormId = "FA_CARD";

                parameter.SelectItems = SelectorItemInfo.CreateItems(
                    "FAssetID",
                    "FNumber",
                    "FName",
                    "FUnitID",
                    "FUnitID.FName",
                    "FQuantity",
                    "FAssetTypeID",
                    "FAssetTypeID.FName",
                    "FAcctDate",
                    "FAllocUseDeptID",
                    "FAllocUseDeptID.FName",
                    "FAssetNO",
                    "FSupplierID",
                    "FSupplierID.FName",
                    "FPositionID",
                    "FPositionID.FName",
                    "FKEEPERID",
                    "FKEEPERID.FName",
                    "F_BHD_Text_xzcbm",
                    "FAllocUseDeptID.F_BHD_Costcenter",
                    "FSpecification"

                );

                string ids = string.Join(",", billIds);
                parameter.FilterClauseWihtKey = $"falterid  IN ({ids})";

                DynamicObjectCollection dataCollection = QueryServiceHelper.GetDynamicObjectCollection(
                    this.Context,
                    parameter
                );

                if (dataCollection != null && dataCollection.Count > 0)
                {
                    // 调用Service的新方法转换
                    return GetAssetCardDataFromQuery(dataCollection);
                }
                else
                {
                    this.View.ShowMessage($"未查询到卡片数据，请检查ID：{ids}", MessageBoxType.Error);
                }
            }
            catch (Exception ex)
            {
                this.View.ShowErrMessage($"批量加载卡片数据失败：{ex.Message}");
            }

            return new List<AssetCardModel>();
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

                // 设备名称 - 主表字段
                if (item["FName"] != null)
                {
                    asset.devName = item["FName"].ToString();
                }

                // 规格型号 - 明细字段
                    if (item["FSpecification"] != null)
                {
                    asset.specificationAndModel = item["FSpecification"].ToString();
                }

                // 设备编码 - 明细字段
                if (item["F_BHD_Text_xzcbm"] != null && item["F_BHD_Text_xzcbm"].ToString() != "")
                {
                    asset.devCode = item["F_BHD_Text_xzcbm"].ToString();
                }
                else
                {
                    asset.devCode = item["FAssetNO"].ToString();
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
                if (item["FAllocUseDeptID_F_BHD_Costcenter"] != null)
                {
                    asset.departmentName = item["FAllocUseDeptID_F_BHD_Costcenter"].ToString();
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
        /// 批量推送资产卡片
        /// </summary>
        private void ProcessBatchPushWithModel(List<AssetCardModel> assetDataList)
        {
            if (assetDataList == null || assetDataList.Count == 0)
            {
                this.View.ShowMessage("没有需要推送的资产卡片", MessageBoxType.Notice);
                return;
            }

            string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(assetDataList, new Newtonsoft.Json.JsonSerializerSettings
            {
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                DateFormatString = "yyyy-MM-dd"
            });

            try
            {
                string responseData = _pushService.SendHttpRequest(jsonData);
                string errorMsg;

                if (_pushService.HandleResponse(responseData, out errorMsg))
                {
                    this.View.ShowMessage($"MES推送成功！共处理{assetDataList.Count}条记录", MessageBoxType.Notice);
                }
                else
                {
                    this.View.ShowMessage($"MES推送失败：{errorMsg}", MessageBoxType.Error);
                }
            }
            catch (Exception ex)
            {
                this.View.ShowMessage($"推送异常：{ex.Message}", MessageBoxType.Error);
            }
        }
    }
}