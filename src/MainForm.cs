using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AntigravityRussian {
    public sealed class MainForm : Form {
        readonly Color bg=Color.FromArgb(17,21,26), card=Color.FromArgb(27,33,40), muted=Color.FromArgb(162,174,186), ink=Color.FromArgb(237,243,248), accent=Color.FromArgb(130,235,193);
        readonly PatchService service=new PatchService();
        readonly DiagnosticReport diagnostics=new DiagnosticReport();
        TextBox path;
        Label status, details, version, guidance;
        RichTextBox log;
        Button browse, check, backup, apply, restore, launch, import;
        Button find, updates, support, help;
        ProgressBar progress;
        bool busy;
        Installation current;
        public MainForm() {
            Text="Antigravity Russian"; ClientSize=new Size(1020,810); MinimumSize=new Size(950,840);
            BackColor=bg; ForeColor=ink; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Font;
            StartPosition=FormStartPosition.CenterScreen;
            try { Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(28,20,28,16),ColumnCount=1,RowCount=9,BackColor=bg };
            foreach(int h in new[]{86,100,76,188,50,28,48}) root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,34)); Controls.Add(root);
            var heading=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty };
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); heading.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,160));
            heading.Controls.Add(Label("Antigravity на русском",25,ink),0,0);
            heading.Controls.Add(Label("Перевод интерфейса на русский · резервная копия и откат",10,muted),0,1);
            var badge=Label("RU  /  "+AppInfo.VersionText,11,accent); badge.TextAlign=ContentAlignment.MiddleRight; heading.Controls.Add(badge,1,0); root.Controls.Add(heading,0,0);
            var location=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(16,8,16,8),BackColor=card,ColumnCount=2,RowCount=3,Margin=new Padding(0,0,0,8) };
            location.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); location.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,160));
            location.RowStyles.Add(new RowStyle(SizeType.Absolute,24)); location.RowStyles.Add(new RowStyle(SizeType.Absolute,34)); location.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            location.Controls.Add(Label("УСТАНОВКА ANTIGRAVITY",9,muted),0,0);
            path=new TextBox { ReadOnly=true,Multiline=true,WordWrap=false,Dock=DockStyle.Fill,BackColor=card,ForeColor=ink,BorderStyle=BorderStyle.None,Margin=new Padding(0,6,12,0),AccessibleName="Папка установки Antigravity" };
            location.Controls.Add(path,0,1); browse=Button("Выбрать файл…",false); browse.Click+=Browse; location.Controls.Add(browse,1,1);
            version=Label("Выберите установленное приложение",9,muted); location.Controls.Add(version,0,2); root.Controls.Add(location,0,1);
            var state=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=new Padding(0,3,0,8) };
            state.RowStyles.Add(new RowStyle(SizeType.Absolute,36)); state.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            status=Label("Проверяем установку…",15,accent); details=Label("",10,muted); state.Controls.Add(status,0,0); state.Controls.Add(details,0,1); root.Controls.Add(state,0,2);
            var steps=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty };
            for(int i=0;i<3;i++) steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
            backup=Button("Создать резервную копию",false); apply=Button("Русифицировать",true); restore=Button("Вернуть оригинал",false);
            steps.Controls.Add(Step("01", "Сохранить оригинал", "Проверенная копия текущей сборки останется на этом компьютере.",backup),0,0);
            steps.Controls.Add(Step("02", "Включить русский", "Переведите интерфейс. Чаты, проекты и настройки сохранятся.",apply),1,0);
            steps.Controls.Add(Step("03", "Откатить изменения", "Верните английский интерфейс без установки старой версии.",restore),2,0);
            backup.Click+=async (s,e)=>await Run("Создаём резервную копию…",directory=>service.Backup(directory,Append),"backup");
            apply.Click+=async (s,e)=>await Run("Устанавливаем русский интерфейс…",directory=>service.Apply(directory,Append),"apply");
            restore.Click+=async (s,e)=>await Run("Восстанавливаем оригинал…",directory=>service.Restore(directory,Append),"restore"); root.Controls.Add(steps,0,3);
            var tools=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0,7,0,0) };
            check=Button("Проверить",false); check.Width=150; check.Click+=async(s,e)=>await RefreshInstallation();
            launch=Button("Открыть Antigravity",false); launch.Width=180; launch.Click+=(s,e)=> { try { service.Launch(path.Text); } catch(Exception x) { ShowError(x); } };
            var folder=Button("Папка копий",false); folder.Width=145; folder.Click+=(s,e)=> { try { Directory.CreateDirectory(Path.Combine(service.DataRoot,"backups")); Process.Start(new ProcessStartInfo(Path.Combine(service.DataRoot,"backups")) { UseShellExecute=true }); } catch(Exception x) { ShowError(x); } };
            import=Button("Импорт старой копии…",false); import.Width=210; import.Click+=Import;
            foreach(var tool in new[]{check,launch,folder,import}) tool.Dock=DockStyle.None;
            tools.Controls.AddRange(new Control[]{check,launch,folder,import}); root.Controls.Add(tools,0,4);
            guidance=Label("Перед русификацией или откатом полностью закройте Antigravity, включая значок в трее.",9,muted); root.Controls.Add(guidance,0,5);
            var extras=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0,5,0,0)};
            find=Button("Найти автоматически",false); find.Width=200; find.Click+=async(s,e)=>await Discover();
            updates=Button("Обновления",false); updates.Width=150; updates.Click+=async(s,e)=>await CheckUpdates();
            support=Button("Отчёт разработчику",false); support.Width=210; support.Click+=(s,e)=> {using(var form=new SupportForm(diagnostics.Build(current)))form.ShowDialog(this);};
            help=Button("Инструкция",false); help.Width=145; help.Click+=(s,e)=> {try{Process.Start(new ProcessStartInfo(AppInfo.Repository+"#readme"){UseShellExecute=true});}catch(Exception ex){ShowError(ex);}};
            foreach(var extra in new[]{find,updates,support,help})extra.Dock=DockStyle.None; extras.Controls.AddRange(new Control[]{find,updates,support,help}); root.Controls.Add(extras,0,6);
            var activity=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=new Padding(0,8,0,0) };
            activity.RowStyles.Add(new RowStyle(SizeType.Absolute,24)); activity.RowStyles.Add(new RowStyle(SizeType.Percent,100)); activity.RowStyles.Add(new RowStyle(SizeType.Absolute,5));
            activity.Controls.Add(Label("ЖУРНАЛ ДЕЙСТВИЙ",9,muted),0,0);
            log=new RichTextBox { Dock=DockStyle.Fill,ReadOnly=true,BackColor=Color.FromArgb(12,16,20),ForeColor=muted,BorderStyle=BorderStyle.None,Font=new Font("Segoe UI",9),DetectUrls=false,AccessibleName="Журнал действий" };
            activity.Controls.Add(log,0,1); progress=new ProgressBar { Dock=DockStyle.Fill,Style=ProgressBarStyle.Marquee,Visible=false }; activity.Controls.Add(progress,0,2); root.Controls.Add(activity,0,7);
            var footer=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));
            footer.Controls.Add(Label("Только перевод интерфейса  ·  Поддержка в Telegram: @zak0407",9,muted),0,0);
            var github=new LinkLabel { Text="Проект на GitHub",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,UseCompatibleTextRendering=true,LinkColor=accent,ActiveLinkColor=ink,VisitedLinkColor=accent };
            github.LinkClicked+=(s,e)=>Process.Start(new ProcessStartInfo(AppInfo.Repository) { UseShellExecute=true }); footer.Controls.Add(github,1,0); root.Controls.Add(footer,0,8);
            Shown+=async(s,e)=>await Discover();
            FormClosing+=(s,e)=> { if(busy) { e.Cancel=true; Append("Дождитесь завершения операции перед закрытием окна."); } };
        }
        Label Label(string text,float size,Color color) { return new Label { Text=text,Font=new Font("Segoe UI",size),ForeColor=color,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,UseCompatibleTextRendering=true,AutoEllipsis=false,Margin=Padding.Empty }; }
        Button Button(string text,bool primary) {
            var b=new Button { Text=text,Dock=DockStyle.Fill,Height=36,Width=220,FlatStyle=FlatStyle.Flat,BackColor=primary?accent:Color.FromArgb(39,49,59),ForeColor=primary?bg:ink,Cursor=Cursors.Hand,Margin=new Padding(0,0,8,0),UseVisualStyleBackColor=false,UseCompatibleTextRendering=true,AccessibleName=text };
            b.FlatAppearance.BorderSize=0; return b;
        }
        Control Step(string number,string title,string description,Button action) {
            var p=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(16),BackColor=card,Margin=new Padding(0,0,10,0) };
            foreach(int h in new[]{26,32}) p.RowStyles.Add(new RowStyle(SizeType.Absolute,h)); p.RowStyles.Add(new RowStyle(SizeType.Percent,100)); p.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
            p.Controls.Add(Label(number,11,accent),0,0); p.Controls.Add(Label(title,13,ink),0,1); var desc=Label(description,10,muted); desc.TextAlign=ContentAlignment.TopLeft; p.Controls.Add(desc,0,2); action.Margin=Padding.Empty; p.Controls.Add(action,0,3); return p;
        }
        async void Browse(object sender,EventArgs e) {
            using(var dialog=new OpenFileDialog { Filter="Antigravity.exe|Antigravity.exe",Title="Выберите установленный Antigravity.exe",CheckFileExists=true }) {
                if(dialog.ShowDialog(this)!=DialogResult.OK) return; path.Text=Path.GetDirectoryName(dialog.FileName); await RefreshInstallation();
            }
        }
        async void Import(object sender,EventArgs e) {
            using(var dialog=new OpenFileDialog { Filter="Резервная копия ASAR|*.asar",Title="Выберите оригинальный app.asar прежнего русификатора",CheckFileExists=true }) {
                if(dialog.ShowDialog(this)!=DialogResult.OK) return; var file=dialog.FileName; await Run("Проверяем старую резервную копию…",directory=>service.ImportBackup(directory,file,Append),"import");
            }
        }
        async Task RefreshInstallation() { await Run("Проверяем установку…",directory=>service.Inspect(directory)); }
        async Task Run(string title,Func<string,Installation> action,string operation="inspect") {
            if(busy)return; busy=true; SetButtons(); status.Text=title; progress.Visible=true;
            string selectedDirectory=path.Text;
            try {
                // Capture control values on the UI thread; worker only sees an immutable selection.
                current=await Task.Run(()=>action(selectedDirectory)); service.SaveSelection(current.Directory);
                status.Text=current.Status; status.ForeColor=accent;
                version.Text="Версия "+current.Version+"   ·   Antigravity App";
                details.Text=current.UnknownTranslation?"Импортируйте оригинальную копию прежнего русификатора или восстановите его оригинал.":current.IsRussian?"Оригинал этой сборки сохранён. При необходимости доступен откат.":current.HasBackup?"Следующий шаг — включить русский интерфейс.":"Начните с сохранения оригинала. Проверка и копирование не меняют приложение.";
                Append(current.Status);
                diagnostics.Record(operation,null);
            } catch(Exception ex) {
                current=null;
                try { current=service.Inspect(selectedDirectory); } catch { }
                ShowError(ex);
                diagnostics.Record(operation,ex);
            }
            finally { busy=false; progress.Visible=false; SetButtons(); }
        }
        void SetButtons() {
            browse.Enabled=check.Enabled=!busy; launch.Enabled=!busy&&current!=null;
            backup.Enabled=!busy&&current!=null&&!current.UnknownTranslation;
            apply.Enabled=!busy&&current!=null&&current.HasBackup&&!current.IsRussian&&!current.UnknownTranslation;
            apply.BackColor=apply.Enabled?accent:Color.FromArgb(39,49,59);
            restore.Enabled=!busy&&current!=null&&current.IsRussian;
            import.Enabled=!busy&&current!=null&&current.UnknownTranslation;
            find.Enabled=updates.Enabled=support.Enabled=help.Enabled=!busy;
        }
        async Task Discover() {
            if(busy)return; busy=true; SetButtons(); progress.Visible=true; status.Text="Ищем установленный Antigravity…";
            string found=null;
            try {found=await Task.Run(()=>PatchService.FindInstallation());diagnostics.Record("discover",null);}
            catch(Exception ex){diagnostics.Record("discover",ex);ShowError(ex);}
            finally{busy=false;progress.Visible=false;SetButtons();}
            if(found==null) {current=null;path.Text="";status.Text="Antigravity не найден автоматически";details.Text="Нажмите «Выбрать файл…» и укажите установленный Antigravity.exe.";SetButtons();Append(status.Text);return;}
            path.Text=found;await RefreshInstallation();
        }
        async Task CheckUpdates() {
            if(busy)return;busy=true;SetButtons();progress.Visible=true;
            try {
                Append("Проверяем обновления русификатора на GitHub…");
                var release=await Task.Run(()=>UpdateService.FetchLatest());
                if(!UpdateService.IsNewer(release.Version,AppInfo.Version)){Append("Установлена актуальная версия русификатора "+AppInfo.VersionText+".");diagnostics.Record("update",null);return;}
                if(MessageBox.Show(this,"Доступен русификатор "+release.Version+". Скачать, проверить и перезапустить приложение?\nРезервные копии и Antigravity не изменяются.","Обновление русификатора",MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
                string plan=await Task.Run(()=>UpdateService.Stage(release,Append));
                UpdateService.StartInstaller(plan);diagnostics.Record("update",null);busy=false;Close();
            }catch(Exception ex){diagnostics.Record("update",ex);Append("Обновление не выполнено: "+ex.Message);MessageBox.Show(this,"Не удалось обновиться: "+ex.Message+"\nМожно скачать ZIP вручную из Releases.","Обновление русификатора",MessageBoxButtons.OK,MessageBoxIcon.Information);}
            finally{if(!IsDisposed){busy=false;progress.Visible=false;SetButtons();}}
        }
        void ShowError(Exception error) {
            diagnostics.Record("operation",error);
            string message=error is UnauthorizedAccessException?"Нет доступа к папке Antigravity. Для установки в Program Files запустите русификатор от имени администратора.":error.Message;
            status.Text="Действие не выполнено"; status.ForeColor=Color.FromArgb(255,175,143); details.Text=message; Append("Ошибка: "+message);
        }
        void Append(string message) {
            if(IsDisposed)return;
            if(InvokeRequired) { BeginInvoke(new Action<string>(Append),message); return; }
            log.AppendText(DateTime.Now.ToString("HH:mm")+"  "+message+Environment.NewLine); log.SelectionStart=log.TextLength; log.ScrollToCaret();
        }
    }
}
