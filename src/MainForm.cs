using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AntigravityRussian {
    public sealed class MainForm : Form {
        readonly Color bg=Color.FromArgb(17,21,26), card=Color.FromArgb(27,33,40), muted=Color.FromArgb(162,174,186), ink=Color.FromArgb(237,243,248), accent=Color.FromArgb(130,235,193), warn=Color.FromArgb(255,196,120), danger=Color.FromArgb(255,175,143), quiet=Color.FromArgb(39,49,59);
        readonly PatchService service=new PatchService();
        readonly DiagnosticReport diagnostics=new DiagnosticReport();
        readonly AgentLanguageRule rule=new AgentLanguageRule();
        readonly AppSettings settings;
        TextBox path;
        Label status, details, version, bannerText;
        RichTextBox log;
        Button primary, restore, launch, cancelWait, updates, support, more, bannerButton;
        CheckBox agentRussian, checkUpdates;
        Panel banner;
        ContextMenuStrip menu;
        ToolStripMenuItem importItem;
        ProgressBar progress;
        Timer waitTimer;
        Func<string,Installation> waitingAction; string waitingTitle, waitingOperation;
        ReleaseInfo pendingRelease;
        bool busy, waiting, loadingOptions;
        Installation current;


        public MainForm() {
            settings=AppSettings.Load(service.DataRoot);
            Text="Antigravity на русском"; ClientSize=new Size(940,760); MinimumSize=new Size(900,680);
            BackColor=bg; ForeColor=ink; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Font;
            StartPosition=FormStartPosition.CenterScreen;
            try { Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(24,16,24,12),ColumnCount=1,RowCount=8,BackColor=bg,AutoScroll=true };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,64)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            foreach(int h in new[]{96,176,102,50}) root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,26)); Controls.Add(root);

            var heading=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty };
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute,38)); heading.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));
            heading.Controls.Add(Label("Antigravity на русском",20,ink),0,0);
            heading.Controls.Add(Label("Перевод интерфейса Antigravity · резервная копия и откат",9.5f,muted),0,1);
            var badge=Label("версия "+AppInfo.VersionText,10,accent); badge.TextAlign=ContentAlignment.MiddleRight; heading.Controls.Add(badge,1,0); root.Controls.Add(heading,0,0);

            banner=new Panel { Dock=DockStyle.Top,Height=46,BackColor=Color.FromArgb(28,52,44),Padding=new Padding(14,7,8,7),Margin=new Padding(0,0,0,8),Visible=false };
            bannerButton=Button("Обновить",true); bannerButton.Dock=DockStyle.Right; bannerButton.Width=130; bannerButton.Click+=async(s,e)=>await InstallUpdate(pendingRelease);
            var later=Button("Позже",false); later.Dock=DockStyle.Right; later.Width=90; later.Margin=new Padding(8,0,0,0); later.Click+=(s,e)=>banner.Visible=false;
            bannerText=Label("",10,ink); banner.Controls.Add(bannerText); banner.Controls.Add(later); banner.Controls.Add(bannerButton); root.Controls.Add(banner,0,1);

            var location=Card(3,2); location.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); location.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));
            location.RowStyles.Add(new RowStyle(SizeType.Absolute,20)); location.RowStyles.Add(new RowStyle(SizeType.Absolute,34)); location.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            location.Controls.Add(Label("УСТАНОВКА ANTIGRAVITY",8.5f,muted),0,0);
            path=new TextBox { ReadOnly=true,Dock=DockStyle.Fill,BackColor=card,ForeColor=ink,BorderStyle=BorderStyle.None,Margin=new Padding(0,5,12,0),AccessibleName="Папка установки Antigravity",TabStop=false };
            location.Controls.Add(path,0,1);
            var browse=Button("Изменить…",false); browse.Click+=Browse; location.Controls.Add(browse,1,1);
            version=Label("Ищем установленное приложение…",9,muted); location.Controls.Add(version,0,2); root.Controls.Add(location,0,2);

            var state=Card(4,1); state.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(int h in new[]{34,50}) state.RowStyles.Add(new RowStyle(SizeType.Absolute,h)); state.RowStyles.Add(new RowStyle(SizeType.Percent,100)); state.RowStyles.Add(new RowStyle(SizeType.Absolute,8));
            status=Label("Проверяем установку…",16,accent); details=Label("",10,muted); details.TextAlign=ContentAlignment.TopLeft; details.AutoEllipsis=false;
            state.Controls.Add(status,0,0); state.Controls.Add(details,0,1);
            var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty };
            primary=Button("Русифицировать",true); primary.Width=250; primary.Click+=async(s,e)=>await Primary();
            restore=Button("Вернуть английский",false); restore.Width=190; restore.Click+=async(s,e)=>await Guarded("Возвращаем английский интерфейс…",d=>service.Restore(d,Append),"restore");
            launch=Button("Открыть Antigravity",false); launch.Width=190; launch.Click+=(s,e)=>Launch();
            cancelWait=Button("Отмена",false); cancelWait.Width=120; cancelWait.Visible=false; cancelWait.Click+=(s,e)=>StopWaiting("Ожидание отменено.");
            foreach(var b in new[]{primary,restore,launch,cancelWait}) { b.Dock=DockStyle.None; b.Margin=new Padding(0,0,10,0); }
            actions.Controls.AddRange(new Control[]{primary,restore,launch,cancelWait}); state.Controls.Add(actions,0,2); root.Controls.Add(state,0,3);

            var options=Card(3,1); options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            options.RowStyles.Add(new RowStyle(SizeType.Absolute,20)); for(int i=0;i<2;i++) options.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
            options.Controls.Add(Label("ДОПОЛНИТЕЛЬНО",8.5f,muted),0,0);
            agentRussian=Check("Агент отвечает на русском — правило в настройках агента Antigravity"); options.Controls.Add(agentRussian,0,1);
            checkUpdates=Check("Проверять обновления русификатора при запуске (раз в сутки, только GitHub)"); options.Controls.Add(checkUpdates,0,2);
            agentRussian.CheckedChanged+=(s,e)=>AgentRussianChanged(); checkUpdates.CheckedChanged+=(s,e)=>CheckUpdatesChanged();
            root.Controls.Add(options,0,4);

            var tools=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0,6,0,0) };
            updates=Button("Проверить обновления",false); updates.Width=240; updates.Click+=async(s,e)=>await CheckUpdates(true);
            support=Button("Отчёт разработчику",false); support.Width=190; support.Click+=(s,e)=> { using(var form=new SupportForm(diagnostics.Build(current))) form.ShowDialog(this); };
            more=Button("Ещё ▾",false); more.Width=110; menu=Menu(); more.Click+=(s,e)=>menu.Show(more,new Point(0,more.Height));
            foreach(var b in new[]{updates,support,more}) { b.Dock=DockStyle.None; b.Margin=new Padding(0,0,10,0); }
            tools.Controls.AddRange(new Control[]{updates,support,more}); root.Controls.Add(tools,0,5);

            var activity=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=new Padding(0,6,0,0) };
            activity.RowStyles.Add(new RowStyle(SizeType.Absolute,22)); activity.RowStyles.Add(new RowStyle(SizeType.Percent,100)); activity.RowStyles.Add(new RowStyle(SizeType.Absolute,5));
            activity.Controls.Add(Label("ЖУРНАЛ ДЕЙСТВИЙ",8.5f,muted),0,0);
            log=new RichTextBox { Dock=DockStyle.Fill,ReadOnly=true,BackColor=Color.FromArgb(12,16,20),ForeColor=muted,BorderStyle=BorderStyle.None,Font=new Font("Segoe UI",9),DetectUrls=false,AccessibleName="Журнал действий",MinimumSize=new Size(0,70) };
            activity.Controls.Add(log,0,1); progress=new ProgressBar { Dock=DockStyle.Fill,Style=ProgressBarStyle.Marquee,Visible=false }; activity.Controls.Add(progress,0,2); root.Controls.Add(activity,0,6);

            var footer=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));
            footer.Controls.Add(Label("Только перевод интерфейса  ·  Поддержка в Telegram: @zak0407",8.5f,muted),0,0);
            var github=new LinkLabel { Text="Проект на GitHub",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,UseCompatibleTextRendering=true,LinkColor=accent,ActiveLinkColor=ink,VisitedLinkColor=accent };
            github.LinkClicked+=(s,e)=>Open(AppInfo.Repository); footer.Controls.Add(github,1,0); root.Controls.Add(footer,0,7);

            waitTimer=new Timer { Interval=1000 }; waitTimer.Tick+=async(s,e)=>await WaitTick();
            LoadOptions(); SetButtons();
            Shown+=async(s,e)=> { await Discover(); await UnlockerCheck(); await StartupUpdateCheck(); };
            FormClosing+=(s,e)=> { if(busy && !waiting) { e.Cancel=true; Append("Дождитесь завершения операции перед закрытием окна."); } };
            FormClosed+=(s,e)=>waitTimer.Stop();
        }

        // Dark title bar on Windows 10 2004+ and Windows 11, matching the window.
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        // Small screens and large scaling: fit the working area, the layout scrolls instead.
        protected override void OnLoad(EventArgs e) {
            base.OnLoad(e);
            var area=Screen.FromControl(this).WorkingArea;
            MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width),Math.Min(MinimumSize.Height,area.Height));
            if(Width>area.Width || Height>area.Height) Bounds=new Rectangle(area.Left,area.Top,Math.Min(Width,area.Width),Math.Min(Height,area.Height));
        }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            try { int on=1; if(DwmSetWindowAttribute(Handle,20,ref on,4)!=0) DwmSetWindowAttribute(Handle,19,ref on,4); } catch { }
        }

        TableLayoutPanel Card(int rows,int columns) { return new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(16,10,16,8),BackColor=card,ColumnCount=columns,RowCount=rows,Margin=new Padding(0,0,0,8) }; }
        Label Label(string text,float size,Color color) { return new Label { Text=text,Font=new Font("Segoe UI",size),ForeColor=color,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,UseCompatibleTextRendering=true,AutoEllipsis=true,Margin=Padding.Empty }; }
        CheckBox Check(string text) { return new CheckBox { Text=text,Dock=DockStyle.Fill,ForeColor=ink,FlatStyle=FlatStyle.Flat,UseCompatibleTextRendering=true,Margin=Padding.Empty,AccessibleRole=AccessibleRole.CheckButton }; }
        Button Button(string text,bool main) {
            var b=new Button { Text=text,Dock=DockStyle.Fill,Height=38,Width=200,FlatStyle=FlatStyle.Flat,BackColor=main?accent:quiet,ForeColor=main?bg:ink,Cursor=Cursors.Hand,Margin=Padding.Empty,UseVisualStyleBackColor=false,UseCompatibleTextRendering=true,AccessibleRole=AccessibleRole.PushButton };
            b.FlatAppearance.BorderSize=0; return b;
        }
        ContextMenuStrip Menu() {
            var m=new ContextMenuStrip { ShowImageMargin=false,Font=Font };
            m.Items.Add("Найти Antigravity автоматически",null,async(s,e)=>await Discover());
            m.Items.Add("Проверить установку ещё раз",null,async(s,e)=>await RefreshInstallation());
            m.Items.Add("Выбрать Antigravity.exe вручную…",null,Browse);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Открыть папку резервных копий",null,(s,e)=> { try { Directory.CreateDirectory(Path.Combine(service.DataRoot,"backups")); Open(Path.Combine(service.DataRoot,"backups")); } catch(Exception x) { ShowError(x); } });
            importItem=new ToolStripMenuItem("Импорт копии прежней русификации…",null,Import); m.Items.Add(importItem);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Инструкция",null,(s,e)=>Open(AppInfo.Repository+"/blob/main/docs/QUICKSTART.md"));
            m.Items.Add("Написать автору в Telegram",null,(s,e)=>Open(AppInfo.Telegram));
            m.Items.Add(new ToolStripSeparator());
            // Recommended companion project: works together with the localizer without conflicts.
            m.Items.Add("Анлокер региона confeden/Antigravity",null,(s,e)=>Open(AppInfo.RegionalProject));
            return m;
        }
        void Open(string target) { try { Process.Start(new ProcessStartInfo(target) { UseShellExecute=true }); } catch(Exception x) { ShowError(x); } }

        void LoadOptions() {
            loadingOptions=true;
            agentRussian.Checked=rule.IsInstalled; checkUpdates.Checked=settings.CheckUpdatesOnStart;
            loadingOptions=false;
        }
        void SaveSettings() { try { settings.Save(service.DataRoot); } catch(Exception x) { Append("Не удалось сохранить настройки: "+x.Message); } }
        void AgentRussianChanged() {
            if(loadingOptions) return;
            settings.AgentRussian=agentRussian.Checked; SaveSettings();
            try {
                // The checkbox mirrors the rule file; the saved preference is reapplied on the next russification.
                if(!agentRussian.Checked) { rule.Remove(); Append("Правило о русском языке агента удалено."); }
                else { rule.Install(); Append("Правило добавлено: агент отвечает на русском в новых сообщениях. Оно видно в Antigravity: «Настройки агента» → «Навыки и правила»."); }
                diagnostics.Record("rule",null);
            } catch(Exception x) { diagnostics.Record("rule",x); ShowError(x); }
        }
        void CheckUpdatesChanged() {
            if(loadingOptions) return;
            settings.CheckUpdatesOnStart=checkUpdates.Checked; SaveSettings();
            Append(checkUpdates.Checked?"Обновления будут проверяться при запуске, не чаще раза в сутки.":"Автоматическая проверка обновлений выключена. Кнопка «Проверить обновления» работает как раньше.");
        }
        async void Browse(object sender,EventArgs e) {
            if(busy) return;
            using(var dialog=new OpenFileDialog { Filter="Antigravity.exe|Antigravity.exe",Title="Выберите установленный Antigravity.exe",CheckFileExists=true }) {
                if(dialog.ShowDialog(this)!=DialogResult.OK) return; path.Text=Path.GetDirectoryName(dialog.FileName); await RefreshInstallation();
            }
        }
        async void Import(object sender,EventArgs e) {
            if(busy) return;
            using(var dialog=new OpenFileDialog { Filter="Резервная копия ASAR|*.asar",Title="Выберите оригинальный app.asar прежнего русификатора",CheckFileExists=true }) {
                if(dialog.ShowDialog(this)!=DialogResult.OK) return; var file=dialog.FileName; await Run("Проверяем копию прежней русификации…",directory=>service.ImportBackup(directory,file,Append),"import");
            }
        }
        async Task Primary() {
            if(current==null) { Browse(this,EventArgs.Empty); return; }
            if(current.UnknownTranslation) { Import(this,EventArgs.Empty); return; }
            if(current.TranslationOutdated) { await Guarded("Обновляем перевод…",d=>service.UpdateTranslation(d,Append),"apply"); return; }
            if(current.IsRussian) { Launch(); return; }
            await Guarded("Включаем русский интерфейс…",d=>service.Apply(d,Append),"apply");
        }
        void Launch() {
            try { service.Launch(path.Text); diagnostics.Record("launch",null); Append("Antigravity открывается."); }
            catch(Exception x) { diagnostics.Record("launch",x); ShowError(x); }
        }
        async Task RefreshInstallation() { await Run("Проверяем установку…",directory=>service.Inspect(directory)); }

        // Writing needs Antigravity closed: wait for it instead of failing, never close it ourselves.
        async Task Guarded(string title,Func<string,Installation> action,string operation) {
            if(busy || current==null) return;
            int running=await Task.Run(()=>PatchService.RunningProcesses(path.Text));
            if(running==0) { await Run(title,action,operation); return; }
            waiting=true; busy=true; waitingAction=action; waitingTitle=title; waitingOperation=operation;
            ShowWaiting(running); SetButtons(); waitTimer.Start();
            Append("Antigravity ещё работает. Операция начнётся сама, как только приложение будет закрыто.");
        }
        void ShowWaiting(int running) {
            status.Text="Закройте Antigravity"; status.ForeColor=warn;
            details.Text="Приложение ещё работает (процессов: "+running+"). Закройте окно и значок в трее: правой кнопкой → «Выйти». Если процессы остаются — Диспетчер задач → Antigravity → «Снять задачу». Продолжим автоматически.";
        }
        async Task WaitTick() {
            if(!waiting) return;
            waitTimer.Stop();
            int running=await Task.Run(()=>PatchService.RunningProcesses(path.Text));
            if(!waiting) return;
            if(running>0) { ShowWaiting(running); waitTimer.Start(); return; }
            var action=waitingAction; string title=waitingTitle, operation=waitingOperation;
            waiting=false; busy=false; SetButtons(); Append("Antigravity закрыт.");
            await Run(title,action,operation);
        }
        void StopWaiting(string message) {
            waitTimer.Stop(); waiting=false; busy=false; Append(message);
            if(current!=null) ShowState(); SetButtons();
        }

        async Task Run(string title,Func<string,Installation> action,string operation="inspect") {
            if(busy) return; busy=true; SetButtons(); status.Text=title; status.ForeColor=accent; progress.Visible=true;
            string selectedDirectory=path.Text;
            try {
                // Capture control values on the UI thread; worker only sees an immutable selection.
                current=await Task.Run(()=>action(selectedDirectory)); service.SaveSelection(current.Directory);
                if(operation=="apply" && current.IsRussian) AfterTranslation();
                if(operation=="restore" && !current.IsRussian) { try { if(rule.IsInstalled) { rule.Remove(); Append("Правило языка агента удалено."); } } catch(Exception x) { Append("Правило языка агента не удалено: "+x.Message); } }
                ShowState(); Append(current.Status);
                diagnostics.Record(operation,null);
            } catch(Exception ex) {
                current=null;
                try { current=service.Inspect(selectedDirectory); } catch { }
                diagnostics.Record(operation,ex);
                ShowError(ex);
            }
            finally { busy=false; progress.Visible=false; SetButtons(); }
        }
        void AfterTranslation() {
            if(!settings.AgentRussian || rule.IsInstalled) return;
            try { rule.Install(); Append("Добавлено правило: агент отвечает на русском. Действует в новых сообщениях."); diagnostics.Record("rule",null); }
            catch(Exception x) { diagnostics.Record("rule",x); Append("Правило языка агента не добавлено: "+x.Message); }
        }
        void ShowState() {
            if(agentRussian.Checked!=rule.IsInstalled) { loadingOptions=true; agentRussian.Checked=rule.IsInstalled; loadingOptions=false; }
            // Evidence for support reports: which archive is installed and how many copies are recorded for it.
            if(current.UnknownTranslation) Append("Отпечаток установленного архива: "+current.Hash.Substring(0,12)+"…, записей копий для этой установки: "+current.Records+".");
            status.Text=current.Status; status.ForeColor=current.UnknownTranslation?warn:accent;
            version.Text="Antigravity App, версия "+current.Version;
            details.Text=current.UnknownTranslation?"Antigravity уже изменён другим русификатором. Если это прежний скрипт v3 — импортируйте его оригинальную копию; иначе сначала верните оригинал тем инструментом."
                :current.TranslationOutdated?"Русификатор обновился, и в нём есть новые переводы. Нажмите «Обновить перевод» — оригинал будет восстановлен и переведён заново."
                :current.IsRussian?"Оригинал этой сборки сохранён — вернуть английский можно в любой момент. Чаты, проекты и настройки не затрагиваются."
                :current.EarlierBuild?"Antigravity установил обновление, и перевод нужно включить заново. Оригинал новой сборки сохранится автоматически."
                :"Нажмите «Русифицировать»: оригинал сохранится автоматически, затем интерфейс станет русским. Antigravity должен быть закрыт.";
        }
        void SetButtons() {
            bool idle=!busy;
            primary.Enabled=idle; primary.Text=current==null?"Выбрать Antigravity.exe…":current.UnknownTranslation?"Импортировать копию…":current.TranslationOutdated?"Обновить перевод":current.IsRussian?"Открыть Antigravity":"Русифицировать";
            primary.BackColor=primary.Enabled?accent:quiet; primary.ForeColor=primary.Enabled?bg:muted;
            restore.Visible=current!=null&&current.IsRussian; restore.Enabled=idle;
            launch.Visible=current!=null&&!current.IsRussian; launch.Enabled=idle;
            cancelWait.Visible=waiting;
            if(waiting) { primary.Enabled=false; restore.Enabled=false; launch.Enabled=false; primary.BackColor=quiet; primary.ForeColor=muted; }
            updates.Enabled=support.Enabled=more.Enabled=bannerButton.Enabled=idle;
            if(importItem!=null) importItem.Enabled=current!=null&&current.UnknownTranslation;
        }
        async Task Discover() {
            if(busy) return; busy=true; SetButtons(); progress.Visible=true; status.Text="Ищем установленный Antigravity…"; status.ForeColor=accent;
            string found=null;
            try { found=await Task.Run(()=>PatchService.FindInstallation()); diagnostics.Record("discover",null); }
            catch(Exception ex) { diagnostics.Record("discover",ex); ShowError(ex); }
            finally { busy=false; progress.Visible=false; SetButtons(); }
            if(found==null) { current=null; path.Text=""; status.Text="Antigravity не найден автоматически"; status.ForeColor=warn; version.Text="Укажите установленное приложение"; details.Text="Нажмите «Выбрать Antigravity.exe…» и укажите файл в папке установки. Обычно это %LOCALAPPDATA%\\Programs\\antigravity."; SetButtons(); Append(status.Text); return; }
            path.Text=found; await RefreshInstallation();
        }

        async Task StartupUpdateCheck() {
            if(!settings.UpdateCheckDue(DateTime.UtcNow)) return;
            try {
                var release=await Task.Run(()=>UpdateService.FetchLatest());
                settings.LastUpdateCheckUtc=DateTime.UtcNow.ToString("o"); SaveSettings(); diagnostics.Record("update",null);
                if(UpdateService.IsNewer(release.Version,AppInfo.Version)) ShowBanner(release);
            } catch(Exception ex) { diagnostics.Record("update",ex); Append("Проверить обновления не удалось. Можно повторить кнопкой «Проверить обновления»."); }
        }
        // Read-only hint for people who also use the regional unlocker: its local proxy must be running.
        async Task UnlockerCheck() {
            string problem=null;
            try { problem=await Task.Run(()=>RegionalUnlocker.ProxyProblem()); } catch(Exception) { }
            if(problem==null) return;
            bannerText.Text=problem; bannerButton.Visible=false; banner.BackColor=Color.FromArgb(58,44,24); banner.Visible=true; Append(problem);
        }
        void ShowBanner(ReleaseInfo release) {
            bannerButton.Visible=true; banner.BackColor=Color.FromArgb(28,52,44);
            pendingRelease=release; bannerText.Text="Доступна новая версия русификатора: "+release.Version+". Резервные копии и Antigravity не изменятся.";
            banner.Visible=true; Append("Доступна версия русификатора "+release.Version+".");
        }
        async Task CheckUpdates(bool manual) {
            if(busy) return; busy=true; SetButtons(); progress.Visible=true;
            ReleaseInfo release=null;
            try {
                Append("Проверяем обновления русификатора на GitHub…");
                release=await Task.Run(()=>UpdateService.FetchLatest());
                settings.LastUpdateCheckUtc=DateTime.UtcNow.ToString("o"); SaveSettings(); diagnostics.Record("update",null);
            } catch(Exception ex) { diagnostics.Record("update",ex); Append("Проверка не выполнена: "+ex.Message); if(manual) MessageBox.Show(this,"Не удалось проверить обновления: "+ex.Message+"\nМожно скачать ZIP вручную из Releases на GitHub.","Обновление русификатора",MessageBoxButtons.OK,MessageBoxIcon.Information); }
            finally { busy=false; progress.Visible=false; SetButtons(); }
            if(release==null) return;
            if(!UpdateService.IsNewer(release.Version,AppInfo.Version)) { Append("Установлена актуальная версия русификатора "+AppInfo.VersionText+"."); return; }
            ShowBanner(release);
            if(MessageBox.Show(this,"Доступен русификатор "+release.Version+". Скачать, проверить и перезапустить приложение?\nРезервные копии и Antigravity не изменяются.","Обновление русификатора",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes) await InstallUpdate(release);
        }
        async Task InstallUpdate(ReleaseInfo release) {
            if(busy || release==null) return; busy=true; SetButtons(); progress.Visible=true;
            try {
                string plan=await Task.Run(()=>UpdateService.Stage(release,Append));
                UpdateService.StartInstaller(plan); diagnostics.Record("update",null); busy=false; Close();
            } catch(Exception ex) { diagnostics.Record("update",ex); Append("Обновление не выполнено: "+ex.Message); MessageBox.Show(this,"Не удалось обновиться: "+ex.Message+"\nМожно скачать ZIP вручную из Releases.","Обновление русификатора",MessageBoxButtons.OK,MessageBoxIcon.Information); }
            finally { if(!IsDisposed) { busy=false; progress.Visible=false; SetButtons(); } }
        }

        void ShowError(Exception error) {
            diagnostics.Record("operation",error);
            bool access=error is UnauthorizedAccessException;
            string message=access?"Нет доступа к папке Antigravity. Обычно так бывает при установке в Program Files.":error.Message;
            status.Text="Действие не выполнено"; status.ForeColor=danger; details.Text=message; Append("Ошибка: "+message);
            if(access && MessageBox.Show(this,"Нет доступа к папке Antigravity. Перезапустить русификатор с правами администратора?","Нужны права администратора",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes) {
                try { Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute=true,Verb="runas" }); busy=false; Close(); }
                catch(System.ComponentModel.Win32Exception) { Append("Запуск с правами администратора отменён."); }
            }
        }
        void Append(string message) {
            if(IsDisposed) return;
            if(InvokeRequired) { BeginInvoke(new Action<string>(Append),message); return; }
            log.AppendText(DateTime.Now.ToString("HH:mm")+"  "+message+Environment.NewLine); log.SelectionStart=log.TextLength; log.ScrollToCaret();
        }
    }
}
