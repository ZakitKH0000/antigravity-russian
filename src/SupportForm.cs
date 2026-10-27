using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
namespace AntigravityRussian {
    public sealed class SupportForm : Form {
        public SupportForm(string report) {
            Text="Отчёт разработчику — @zak0407"; ClientSize=new Size(800,570); MinimumSize=new Size(740,570); StartPosition=FormStartPosition.CenterParent;
            BackColor=Color.FromArgb(17,21,26); ForeColor=Color.Gainsboro; Font=new Font("Segoe UI",10);
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=4};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,68)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); Controls.Add(layout);
            layout.Controls.Add(new Label {Text="Поддержка: @zak0407 (Telegram)\nПросмотрите отчёт. Сообщение появится как черновик; отправку нужно подтвердить в Telegram.",Dock=DockStyle.Fill,UseCompatibleTextRendering=true},0,0);
            layout.Controls.Add(new RichTextBox {Text=report,ReadOnly=true,Dock=DockStyle.Fill,BackColor=Color.FromArgb(27,33,40),ForeColor=Color.Gainsboro,BorderStyle=BorderStyle.None,Font=new Font("Consolas",10),DetectUrls=false,AccessibleName="Предпросмотр отчёта"},0,1);
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,8,0,0)}; layout.Controls.Add(buttons,0,2);
            var send=new Button {Text="Открыть Telegram с отчётом",Width=260,Height=34};
            send.Click+=(s,e)=>Try(()=> { Clipboard.SetText(report); Process.Start(new ProcessStartInfo(DiagnosticReport.TelegramDraft(report)) {UseShellExecute=true}); });
            var copy=new Button {Text="Скопировать",Width=145,Height=34}; copy.Click+=(s,e)=>Try(()=>Clipboard.SetText(report));
            var save=new Button {Text="Сохранить TXT…",Width=160,Height=34}; save.Click+=(s,e)=> {
                using(var dialog=new SaveFileDialog {Filter="Текстовый отчёт|*.txt",FileName="AntigravityRussian-report.txt"})if(dialog.ShowDialog(this)==DialogResult.OK)Try(()=>File.WriteAllText(dialog.FileName,report,new System.Text.UTF8Encoding(true)));
            };
            buttons.Controls.AddRange(new Control[]{send,copy,save});
            layout.Controls.Add(new Label {Text="Без чатов, файлов, токенов, имён и путей. Если черновик не открылся — вставьте скопированный текст в чат @zak0407.",Dock=DockStyle.Fill,UseCompatibleTextRendering=true},0,3);
        }
        void Try(Action action) { try {action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Не удалось выполнить действие",MessageBoxButtons.OK,MessageBoxIcon.Information);} }
    }
}
