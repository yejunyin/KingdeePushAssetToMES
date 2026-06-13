using Kingdee.BOS.Core.Bill.PlugIn;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using System;
using System.ComponentModel;

namespace Ben.HL.FIN.FA.Business.PlugIn
{
    [Kingdee.BOS.Util.HotUpdate]
    [Description("Ben-资产卡片审核推送")]
    public class buttonclass : AbstractBillPlugIn
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
                bool success = _pushService.PushToMES(this.Model.DataObject, out message);

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
    }
}