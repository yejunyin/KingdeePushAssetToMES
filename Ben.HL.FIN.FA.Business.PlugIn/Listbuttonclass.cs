using Kingdee.BOS.Core.Bill.PlugIn;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.List.PlugIn;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Ben.HL.FIN.FA.Business.PlugIn
{
    [Kingdee.BOS.Util.HotUpdate]
    [Description("Ben-资产卡片列表审核推送")]
    public class Listbuttonclass : AbstractListPlugIn
    {
        /// <summary>
        /// 工具栏按钮点击事件
        /// </summary>
        public override void AfterDoOperation(AfterDoOperationEventArgs e)
        {
            switch (e.Operation.Operation)
            {
                case "Audit":
                    ExecuteToRelease();
                    break;
                default:
                    break;
            }

            // 取消默认事件处理，由 BeforeDoOperation 来控制是否执行操作
            //e.Cancel = true;
            //return;
        }

        private void ExecuteToRelease()
        {
            try
            {

            }
            catch (WebException ex)
            {
                
                this.View.ShowMessage("网络请求失败：" + ex.Message, MessageBoxType.Error);
            }
            catch (Exception ex)
            {
                this.View.ShowMessage("MES推送失败", MessageBoxType.Error);
            }
        }
    }
}
