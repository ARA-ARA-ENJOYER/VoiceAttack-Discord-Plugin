using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VoiceAttackDiscordPlugin.Setup;

/// <summary>
/// Guided installer for the VoiceAttack Discord Plugin.
/// Walks a non-technical user through: locate VoiceAttack -> install files ->
/// configure bot -> finish. Runs elevated (see app.manifest).
/// </summary>
public sealed class SetupForm : Form
{
    private const string PluginFolderName = "VA.VoiceAttackDiscordPlugin";
    private const string OldFolderName = "VA.DiscordVAPlugin";
    private const string PayloadPrefix = "Payload.";

    // ---------- design tokens: two semantic palettes, dark by default ----------
    // Every text/surface pair below meets 4.5:1 in its own mode (AA).
    private sealed record Theme(
        Color Surface, Color SurfaceAlt, Color SurfaceInput, Color Border,
        Color TextPrimary, Color TextSecondary,
        Color Accent, Color AccentHover,
        Color Success, Color Error,
        Color SecondaryHover, Color SecondaryDown);

    private static readonly Theme Dark = new(
        Surface: Color.FromArgb(0x31, 0x33, 0x38), // Discord dark: primary bg
        SurfaceAlt: Color.FromArgb(0x2B, 0x2D, 0x31), // secondary surfaces (footer, cards)
        SurfaceInput: Color.FromArgb(0x1E, 0x1F, 0x22), // textbox fills
        Border: Color.FromArgb(0x3B, 0x3D, 0x44),
        TextPrimary: Color.FromArgb(0xF2, 0xF3, 0xF5),
        TextSecondary: Color.FromArgb(0xB5, 0xBA, 0xC1),
        Accent: Color.FromArgb(0x58, 0x65, 0xF2), // Discord blurple, both modes
        AccentHover: Color.FromArgb(0x47, 0x52, 0xC4),
        Success: Color.FromArgb(0x57, 0xF2, 0x87),
        Error: Color.FromArgb(0xED, 0x42, 0x45),
        SecondaryHover: Color.FromArgb(0x3A, 0x3C, 0x41),
        SecondaryDown: Color.FromArgb(0x40, 0x42, 0x49));

    private static readonly Theme Light = new(
        Surface: Color.White,
        SurfaceAlt: Color.FromArgb(0xF7, 0xF8, 0xFA),
        SurfaceInput: Color.White,
        Border: Color.FromArgb(0xE4, 0xE7, 0xEC),
        TextPrimary: Color.FromArgb(0x1F, 0x23, 0x29),
        TextSecondary: Color.FromArgb(0x4A, 0x55, 0x68),
        Accent: Color.FromArgb(0x58, 0x65, 0xF2),
        AccentHover: Color.FromArgb(0x47, 0x52, 0xC4),
        Success: Color.FromArgb(0x1E, 0x8E, 0x3E),
        Error: Color.FromArgb(0xC6, 0x28, 0x28),
        SecondaryHover: Color.FromArgb(0xEF, 0xF1, 0xF6),
        SecondaryDown: Color.FromArgb(0xE4, 0xE7, 0xEC));

    private Theme _t = Dark;
    private StatusKind _statusKind = StatusKind.None;
    private string _statusText = "";
    private string _statusPrefix = "";

    // Control -> theme role; ApplyTheme re-colors every registered control.
    private readonly Dictionary<Control, string> _roles = new();

    private static readonly string[] StepNames = { "Welcome", "Location", "Install", "Configure", "Done" };

    private readonly Label _title = new();
    private readonly Label _stepLabel = new();
    private readonly StepIndicator _steps = new();
    private readonly Panel _body = new();
    private readonly Panel _footer = new();
    private readonly Button _back = new();
    private readonly Button _next = new();

    private readonly Panel _stepWelcome = new();
    private readonly Panel _stepLocation = new();
    private readonly Panel _stepInstall = new();
    private readonly Panel _stepConfigure = new();
    private readonly Panel _stepFinish = new();

    private readonly TextBox _vaPath = new();
    private readonly TextBox _installLog = new();
    private readonly CheckBox _removeOld = new();
    private readonly TextBox _token = new();
    private readonly TextBox _guildId = new();
    private readonly TextBox _channel = new();
    private readonly TextBox _instructions = new();
    private readonly Button _invite = new();
    private readonly Label _tokenStatus = new();
    private readonly Button _save = new();
    private readonly Label _finishHead = new();
    private readonly Label _finishSummary = new();
    private readonly Button _themeToggle = new();
    private enum StatusKind { None, Info, Ok, Err }
    private string? _preservedEncrypted;
    private string? _preservedEncryptedBackup;
    private bool _existingAutoConnect = true;
    private string _existingLogLevel = "Info";
    private string? _clientId;

    // Bot tokens are three dot-separated segments; mirrors the plugin's TokenValidator.
    private static readonly Regex TokenPattern = new(@"^[\w-]+\.[\w-]+\.[\w-]+$", RegexOptions.Compiled);
    private bool _tokenValid;

    private int _step;
    private string _installDir = "";

    public SetupForm()
    {
        Text = "VoiceAttack Discord Plugin — Setup";
        ClientSize = new Size(560, 486);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        _t = Dark;

        // Header: title + blurple accent rule + theme toggle.
        _title.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        _title.Text = "VoiceAttack Discord Plugin";
        _title.SetBounds(16, 10, 420, 28);
        Role(_title, "text");
        Controls.Add(_title);

        _themeToggle.SetBounds(444, 12, 100, 26);
        Role(_themeToggle, "secondary");
        _themeToggle.Click += (_, _) => ToggleTheme();
        Controls.Add(_themeToggle);

        var rule = new Panel { BackColor = _t.Accent };
        rule.SetBounds(16, 42, 60, 3);
        Role(rule, "accentFill");
        Controls.Add(rule);

        _stepLabel.SetBounds(16, 50, 528, 20);
        Role(_stepLabel, "muted");
        Controls.Add(_stepLabel);

        _steps.SetBounds(16, 72, 528, 40);
        Controls.Add(_steps);

        // Body.
        _body.SetBounds(16, 118, 528, 288);
        Controls.Add(_body);

        // Footer band with a 1px top separator.
        var sep = new Panel { Dock = DockStyle.Top, Height = 1 };
        Role(sep, "sep");
        _footer.Controls.Add(sep);
        Role(_footer, "surfaceAlt");
        _footer.SetBounds(0, 416, 560, 70);
        Controls.Add(_footer);

        _back.Text = "< Back";
        StyleSecondary(_back);
        _back.SetBounds(16, 18, 110, 34);
        _back.Click += (_, _) => ShowStep(_step - 1);
        _footer.Controls.Add(_back);

        _next.Text = "Next >";
        StylePrimary(_next);
        _next.SetBounds(434, 18, 110, 34);
        _next.Click += (_, _) => OnNext();
        _footer.Controls.Add(_next);
        AcceptButton = _next;

        BuildWelcomeStep();
        BuildLocationStep();
        BuildInstallStep();
        BuildConfigureStep();
        BuildFinishStep();

        foreach (var p in new[] { _stepWelcome, _stepLocation, _stepInstall, _stepConfigure, _stepFinish })
        {
            p.Dock = DockStyle.Fill;
            p.Visible = false;
            Role(p, "panel");
            _body.Controls.Add(p);
        }

        var detected = DetectVoiceAttack();
        if (detected != null) _vaPath.Text = detected;

        LoadExistingConfig();

        ApplyTheme();
        ShowStep(0);
    }

    // ---------- theming ----------

    private void Role(Control c, string role)
    {
        _roles[c] = role;
        ApplyRole(c, role);
    }

    private void ApplyRole(Control c, string role)
    {
        switch (role)
        {
            case "panel": c.BackColor = _t.Surface; break;
            case "surfaceAlt": c.BackColor = _t.SurfaceAlt; break;
            case "sep": c.BackColor = _t.Border; break;
            case "accentFill": c.BackColor = _t.Accent; break;
            case "text": c.ForeColor = _t.TextPrimary; break;
            case "check": c.ForeColor = _t.TextPrimary; break;
            case "muted": c.ForeColor = _t.TextSecondary; break;
            case "accentText": c.ForeColor = _t.Accent; break;
            case "badge": c.BackColor = _t.Accent; c.ForeColor = Color.White; break;
            case "card": c.BackColor = _t.SurfaceAlt; c.ForeColor = _t.TextPrimary; break;
            case "field":
            case "log": c.BackColor = _t.SurfaceInput; c.ForeColor = _t.TextPrimary; break;
            case "primary" when c is Button b:
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 0;
                b.BackColor = _t.Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.MouseOverBackColor = _t.AccentHover;
                b.FlatAppearance.MouseDownBackColor = _t.AccentHover;
                b.Cursor = Cursors.Hand;
                break;
            case "secondary" when c is Button b:
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = _t.Border;
                b.BackColor = _t.SurfaceAlt;
                b.ForeColor = _t.TextPrimary;
                b.FlatAppearance.MouseOverBackColor = _t.SecondaryHover;
                b.FlatAppearance.MouseDownBackColor = _t.SecondaryDown;
                b.Cursor = Cursors.Hand;
                break;
        }
    }

    private void ApplyTheme()
    {
        BackColor = _t.Surface;
        _body.BackColor = _t.Surface;
        foreach (var (c, role) in _roles) ApplyRole(c, role);
        RenderStatus();
        _steps.Theme = _t;
        _steps.Invalidate();
        _themeToggle.Text = ReferenceEquals(_t, Dark) ? "☀ Light" : "🌙 Dark";
        _themeToggle.AccessibleName = ReferenceEquals(_t, Dark) ? "Switch to light mode" : "Switch to dark mode";
    }

    private void ToggleTheme()
    {
        _t = ReferenceEquals(_t, Dark) ? Light : Dark;
        ApplyTheme();
    }

    private void RenderStatus()
    {
        _tokenStatus.Text = _statusPrefix + _statusText;
        _tokenStatus.ForeColor = _statusKind switch
        {
            StatusKind.Ok => _t.Success,
            StatusKind.Err => _t.Error,
            StatusKind.Info => _t.TextSecondary,
            _ => _t.TextSecondary
        };
    }

    // ---------- shared styling ----------

    private void StylePrimary(Button b)
    {
        Role(b, "primary");
        b.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
    }

    private void StyleSecondary(Button b)
    {
        Role(b, "secondary");
    }

    private Label StepHeading(string text)
    {
        var lbl = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            AutoSize = false
        };
        Role(lbl, "text");
        return lbl;
    }

    private Label FieldLabel(string text)
    {
        var lbl = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            AutoSize = false
        };
        Role(lbl, "text");
        return lbl;
    }

    private Label Muted(string text)
    {
        var lbl = new Label
        {
            Text = text,
            AutoSize = false
        };
        Role(lbl, "muted");
        return lbl;
    }

    private void StyleField(TextBox t)
    {
        t.BorderStyle = BorderStyle.FixedSingle;
        Role(t, "field");
    }

    // ---------- step 0: welcome ----------

    private void BuildWelcomeStep()
    {
        var lead = new Label
        {
            Text = "This wizard installs the plugin and helps you connect your Discord bot. " +
                   "It takes about five minutes.",
            AutoSize = false
        };
        Role(lead, "text");
        lead.SetBounds(0, 4, 528, 40);
        _stepWelcome.Controls.Add(lead);

        string[] heads = { "Find VoiceAttack", "Install the plugin files", "Configure your Discord bot" };
        string[] subs =
        {
            "Your install folder is detected automatically.",
            "Needs admin rights — that's the prompt you just saw.",
            "Guided: paste a token, click Validate, invite the bot, done."
        };
        for (int i = 0; i < 3; i++)
        {
            var num = new Label
            {
                Text = (i + 1).ToString(),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.TopCenter
            };
            Role(num, "accentText");
            num.SetBounds(4, 52 + i * 62, 32, 56);
            _stepWelcome.Controls.Add(num);

            var head = StepHeading(heads[i]);
            head.SetBounds(40, 52 + i * 62, 488, 22);
            _stepWelcome.Controls.Add(head);

            var sub = Muted(subs[i]);
            sub.SetBounds(40, 74 + i * 62, 488, 24);
            _stepWelcome.Controls.Add(sub);
        }

        var note = Muted("You'll need a Discord bot token. Don't have one yet? No problem — the wizard points you to the right page.");
        note.SetBounds(0, 244, 528, 40);
        _stepWelcome.Controls.Add(note);
    }

    // ---------- step 1: locate VoiceAttack ----------

    private void BuildLocationStep()
    {
        var lbl = StepHeading("Where is VoiceAttack installed?");
        lbl.SetBounds(0, 8, 528, 24);
        _stepLocation.Controls.Add(lbl);

        StyleField(_vaPath);
        _vaPath.SetBounds(0, 40, 408, 28);
        _stepLocation.Controls.Add(_vaPath);

        var browse = new Button { Text = "Browse…" };
        StyleSecondary(browse);
        browse.SetBounds(416, 38, 112, 32);
        browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Select your VoiceAttack folder (contains VoiceAttack.exe)" };
            if (dlg.ShowDialog() == DialogResult.OK) _vaPath.Text = dlg.SelectedPath;
        };
        _stepLocation.Controls.Add(browse);

        var hint = Muted("Usually C:\\Program Files\\VoiceAttack or C:\\Program Files (x86)\\VoiceAttack. " +
                         "The wizard detected it automatically when possible — just check it looks right.");
        hint.SetBounds(0, 80, 528, 44);
        _stepLocation.Controls.Add(hint);
    }

    private static string? DetectVoiceAttack()
    {
        foreach (var baseDir in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        })
        {
            var candidate = Path.Combine(baseDir, "VoiceAttack");
            if (File.Exists(Path.Combine(candidate, "VoiceAttack.exe")))
                return candidate;
        }
        return null;
    }

    // ---------- step 2: install ----------

    private void BuildInstallStep()
    {
        var lbl = StepHeading("Install the plugin files");
        lbl.SetBounds(0, 8, 528, 24);
        _stepInstall.Controls.Add(lbl);

        _removeOld.Text = "Remove the old VA.DiscordVAPlugin folder if present (recommended)";
        _removeOld.Checked = true;
        _removeOld.AutoSize = false;
        Role(_removeOld, "check");
        _removeOld.SetBounds(0, 38, 528, 26);
        _stepInstall.Controls.Add(_removeOld);

        _installLog.Multiline = true;
        _installLog.ReadOnly = true;
        _installLog.ScrollBars = ScrollBars.Vertical;
        _installLog.BorderStyle = BorderStyle.FixedSingle;
        Role(_installLog, "log");
        _installLog.Font = new Font(FontFamily.GenericMonospace, 9F);
        _installLog.SetBounds(0, 70, 528, 218);
        _stepInstall.Controls.Add(_installLog);
    }

    private void RunInstall()
    {
        _installLog.Clear();
        try
        {
            _installDir = Path.Combine(_vaPath.Text.Trim(), "Apps", PluginFolderName);
            Directory.CreateDirectory(_installDir);

            int count = 0;
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (!name.StartsWith(PayloadPrefix, StringComparison.Ordinal)) continue;
                var fileName = name.Substring(PayloadPrefix.Length);
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) continue;
                using var file = File.Create(Path.Combine(_installDir, fileName));
                stream.CopyTo(file);
                _installLog.AppendText($"Installed {fileName}\r\n");
                count++;
            }

            if (count == 0)
            {
                _installLog.AppendText("ERROR: no embedded plugin files found. The setup build is broken.\r\n");
                return;
            }

            // Migrate config + clean up the legacy folder
            var oldDir = Path.Combine(_vaPath.Text.Trim(), "Apps", OldFolderName);
            var newConfig = Path.Combine(_installDir, "config.json");
            if (Directory.Exists(oldDir))
            {
                var oldConfig = Path.Combine(oldDir, "config.json");
                if (File.Exists(oldConfig) && !File.Exists(newConfig))
                {
                    File.Copy(oldConfig, newConfig);
                    _installLog.AppendText("Copied your existing config.json (token preserved).\r\n");
                }
                if (_removeOld.Checked)
                {
                    Directory.Delete(oldDir, recursive: true);
                    _installLog.AppendText($"Removed old folder {OldFolderName}.\r\n");
                }
            }

            _installLog.AppendText($"\r\nDone — {count} files in:\r\n{_installDir}\r\n");
        }
        catch (Exception ex)
        {
            _installLog.AppendText($"ERROR: {ex.Message}\r\n");
        }
    }

    // ---------- step 3: configure ----------

    private void BuildConfigureStep()
    {
        _stepConfigure.AutoScroll = true;
        _stepConfigure.AutoScrollMinSize = new Size(508, 430);

        var head = StepHeading("Set up the Discord side");
        head.SetBounds(0, 4, 508, 24);
        _stepConfigure.Controls.Add(head);

        var sub = Muted("Do each step once, in order — most of it is copy-pasting between Discord's site and this box.");
        sub.SetBounds(0, 30, 508, 20);
        _stepConfigure.Controls.Add(sub);

        _instructions.Multiline = true;
        _instructions.ReadOnly = true;
        _instructions.ScrollBars = ScrollBars.Vertical;
        _instructions.WordWrap = true;
        _instructions.BorderStyle = BorderStyle.FixedSingle;
        Role(_instructions, "card");
        _instructions.Text =
            "1. Click \"Open Discord Developer Portal\", then New Application → name it → Create.\r\n" +
            "2. Go to Bot → Reset Token → Copy. That's the bot token below.\r\n" +
            "3. Same Bot page → Privileged Gateway Intents → enable Server Members + Message Content → Save Changes.\r\n" +
            "4. Back here: click Validate token, then \"Invite bot to this server\" and pick your server.\r\n" +
            "5. In Discord, right-click your server icon → Copy Server ID, and paste it below. " +
            "(If it's greyed out: Settings → Advanced → turn on Developer Mode.)\r\n" +
            "6. Default channel is optional — that's where messages land (e.g. general).";
        _instructions.SetBounds(0, 56, 508, 112);
        _stepConfigure.Controls.Add(_instructions);

        var portal = new Button { Text = "Open Discord Developer Portal" };
        StyleSecondary(portal);
        portal.SetBounds(0, 176, 250, 32);
        portal.Click += (_, _) => Process.Start(new ProcessStartInfo("https://discord.com/developers/applications") { UseShellExecute = true });
        _stepConfigure.Controls.Add(portal);

        _invite.Text = "Invite bot to this server";
        StyleSecondary(_invite);
        _invite.Enabled = false;
        _invite.SetBounds(258, 176, 250, 32);
        _invite.Click += (_, _) => InviteBot();
        _stepConfigure.Controls.Add(_invite);

        var tokenLbl = FieldLabel("Bot token:");
        tokenLbl.SetBounds(0, 216, 508, 20);
        _stepConfigure.Controls.Add(tokenLbl);

        _token.UseSystemPasswordChar = true;
        StyleField(_token);
        _token.SetBounds(0, 238, 508, 28);
        _token.TextChanged += (_, _) =>
        {
            _tokenValid = false;
            if (_token.Text.Trim().Length > 0)
            {
                _preservedEncrypted = null;
                SetStatus("", StatusKind.None, "");
            }
            else if (_preservedEncryptedBackup != null)
            {
                // Box cleared again: restore the untouched encrypted blob so a
                // stray keystroke can't strand a working existing token.
                _preservedEncrypted = _preservedEncryptedBackup;
                SetStatus("Token already configured & encrypted on this PC.", StatusKind.Ok, "\u2713 ");
            }
            else
            {
                SetStatus("", StatusKind.None, "");
            }
            _clientId = null;
            _invite.Enabled = false;
            _save.Enabled = CanSave();
        };
        _stepConfigure.Controls.Add(_token);

        var validate = new Button { Text = "Validate token" };
        StyleSecondary(validate);
        validate.SetBounds(0, 274, 150, 32);
        validate.Click += async (_, _) => await ValidateTokenAsync();
        _stepConfigure.Controls.Add(validate);

        _tokenStatus.AutoSize = false;
        _tokenStatus.SetBounds(158, 274, 350, 32);
        _stepConfigure.Controls.Add(_tokenStatus);

        var guildLbl = FieldLabel("Server (guild) ID:");
        guildLbl.SetBounds(0, 314, 244, 20);
        _stepConfigure.Controls.Add(guildLbl);

        StyleField(_guildId);
        _guildId.SetBounds(0, 336, 244, 28);
        _stepConfigure.Controls.Add(_guildId);

        var channelLbl = FieldLabel("Default channel (optional):");
        channelLbl.SetBounds(264, 314, 244, 20);
        _stepConfigure.Controls.Add(channelLbl);

        _channel.Text = "general";
        StyleField(_channel);
        _channel.SetBounds(264, 336, 244, 28);
        _stepConfigure.Controls.Add(_channel);

        var save = _save;
        save.Text = "Save config.json";
        StylePrimary(save);
        save.Enabled = false;
        save.SetBounds(0, 372, 180, 34);
        save.Click += (_, _) => SaveConfig();
        _stepConfigure.Controls.Add(save);

        var note = Muted("config.json stays only on this PC — the plugin encrypts your token on first run.");
        note.SetBounds(188, 372, 320, 34);
        _stepConfigure.Controls.Add(note);
    }

    private async Task ValidateTokenAsync()
    {
        var token = _token.Text.Trim();
        if (token.Length == 0)
        {
            SetStatus("Paste your bot token first.", StatusKind.Err, "\u26a0 ");
            return;
        }

        if (!TokenPattern.IsMatch(token))
        {
            SetStatus("That doesn't look like a bot token (three parts separated by dots).", StatusKind.Err, "\u26a0 ");
            _tokenValid = false;
            _save.Enabled = false;
            return;
        }

        SetStatus("Checking with Discord…", StatusKind.Info, "");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bot", token);
            using var res = await http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                SetStatus($"Invalid token (Discord returned {(int)res.StatusCode}).", StatusKind.Err, "\u26a0 ");
                _tokenValid = false;
                _save.Enabled = false;
                return;
            }
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var name = doc.RootElement.TryGetProperty("username", out var u) ? u.GetString() : "?";
            _clientId = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            _invite.Enabled = !string.IsNullOrEmpty(_clientId);
            SetStatus($"Token valid — bot is \"{name}\".", StatusKind.Ok, "\u2713 ");
            _tokenValid = true;
            _save.Enabled = true;
        }
        catch (Exception ex)
        {
            SetStatus($"Could not reach Discord: {ex.Message}", StatusKind.Err, "\u26a0 ");
            _tokenValid = false;
            _save.Enabled = false;
        }
    }

    private void SetStatus(string text, StatusKind kind, string prefix)
    {
        _statusText = text;
        _statusPrefix = prefix;
        _statusKind = kind;
        RenderStatus();
    }

    private void InviteBot()
    {
        if (string.IsNullOrEmpty(_clientId))
        {
            MessageBox.Show("Validate your bot token first, then invite the bot.", "Setup",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Least privilege for what the plugin actually does: view/send/read (10,11,16),
        // voice connect+speak+VAD (20,21,25), and mute/deafen members (22,23) which
        // botmute/botdeafen need for their server-side ModifyAsync calls.
        long permissions = (1L << 10) | (1L << 11) | (1L << 16) | (1L << 20) | (1L << 21) | (1L << 22) | (1L << 23) | (1L << 25);
        var url = $"https://discord.com/oauth2/authorize?client_id={_clientId}&scope=bot&permissions={permissions}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private bool CanSave()
    {
        var token = _token.Text.Trim();
        if (token.Length == 0) return _preservedEncrypted != null;
        return _tokenValid && TokenPattern.IsMatch(token);
    }

    private void SaveConfig()
    {
        var token = _token.Text.Trim();
        if (token.Length > 0 && (!_tokenValid || !TokenPattern.IsMatch(token)))
        {
            MessageBox.Show("Click \"Validate token\" first — only a token Discord approved can be saved.", "Setup",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (token.Length == 0 && _preservedEncrypted == null)
        {
            MessageBox.Show("Paste your bot token first (step 2).", "Setup",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!ulong.TryParse(_guildId.Text.Trim(), out var guildId))
        {
            MessageBox.Show("Enter a numeric server ID first (step 5).", "Setup",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var channel = _channel.Text.Trim();
        if (channel.Length == 0) channel = "general";

        try
        {
            // Serialized (never string-concatenated) so any token text stays valid JSON.
            // A fresh token stays plaintext; the plugin encrypts it on first load
            // (DPAPI, this Windows user). An untouched existing token keeps its blob.
            var payload = new Dictionary<string, object?>
            {
                ["BotToken"] = token,
                ["EncryptedBotToken"] = token.Length == 0 ? _preservedEncrypted : "",
                ["DefaultGuildId"] = guildId,
                ["DefaultChannelName"] = channel,
                ["AutoConnect"] = _existingAutoConnect,
                ["LogLevel"] = _existingLogLevel
            };
            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(_installDir, "config.json"), json);
            MessageBox.Show("config.json saved. Your token stays on this PC only, and the plugin encrypts it on first run.", "Setup",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save config.json:\n{ex.Message}", "Setup",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadExistingConfig()
    {
        string configPath = Path.Combine(_vaPath.Text.Trim(), "Apps", PluginFolderName, "config.json");
        if (!File.Exists(configPath)) return;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            var root = doc.RootElement;

            if (root.TryGetProperty("EncryptedBotToken", out var enc) && !string.IsNullOrWhiteSpace(enc.GetString()))
            {
                _preservedEncrypted = enc.GetString();
                _preservedEncryptedBackup = _preservedEncrypted;
                SetStatus("Token already configured & encrypted on this PC.", StatusKind.Ok, "\u2713 ");
                _save.Enabled = CanSave();
            }
            else if (root.TryGetProperty("BotToken", out var bot) && !string.IsNullOrWhiteSpace(bot.GetString()))
            {
                _token.Text = bot.GetString();
                _tokenValid = true;
                SetStatus("Loaded your saved token. Save to keep using it.", StatusKind.Ok, "\u2713 ");
                _save.Enabled = CanSave();
            }

            if (root.TryGetProperty("DefaultGuildId", out var g) && g.TryGetInt64(out var gid) && gid > 0)
                _guildId.Text = gid.ToString();
            if (root.TryGetProperty("DefaultChannelName", out var ch) && !string.IsNullOrWhiteSpace(ch.GetString()))
                _channel.Text = ch.GetString();
            // A re-run over an existing config must not reset these to defaults.
            if (root.TryGetProperty("AutoConnect", out var ac) &&
                (ac.ValueKind == JsonValueKind.True || ac.ValueKind == JsonValueKind.False))
                _existingAutoConnect = ac.GetBoolean();
            if (root.TryGetProperty("LogLevel", out var ll) && !string.IsNullOrWhiteSpace(ll.GetString()))
                _existingLogLevel = ll.GetString()!;
        }
        catch
        {
            // Not fatal — the wizard simply starts blank.
        }
    }

    // ---------- step 4: finish ----------

    private void BuildFinishStep()
    {
        var badge = new Label
        {
            Text = "\u2713",
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false
        };
        Role(badge, "badge");
        badge.SetBounds(0, 8, 48, 48);
        _stepFinish.Controls.Add(badge);

        _finishHead.Text = "Setup complete";
        _finishHead.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        Role(_finishHead, "text");
        _finishHead.AutoSize = false;
        _finishHead.SetBounds(60, 8, 468, 28);
        _stepFinish.Controls.Add(_finishHead);

        var sub = Muted("Your Discord bot is connected and ready for voice commands.");
        sub.SetBounds(60, 36, 468, 22);
        _stepFinish.Controls.Add(sub);

        _finishSummary.AutoSize = false;
        Role(_finishSummary, "text");
        _finishSummary.SetBounds(0, 70, 528, 150);
        _stepFinish.Controls.Add(_finishSummary);

        var openVa = new Button { Text = "Open VoiceAttack" };
        StylePrimary(openVa);
        openVa.SetBounds(0, 228, 220, 36);
        openVa.Click += (_, _) =>
        {
            var exe = Path.Combine(_vaPath.Text.Trim(), "VoiceAttack.exe");
            if (File.Exists(exe))
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            else
                MessageBox.Show("VoiceAttack.exe was not found at that location.", "Setup",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        _stepFinish.Controls.Add(openVa);
    }

    // ---------- navigation ----------

    private void ShowStep(int step)
    {
        _step = Math.Clamp(step, 0, 4);
        _stepWelcome.Visible = _step == 0;
        _stepLocation.Visible = _step == 1;
        _stepInstall.Visible = _step == 2;
        _stepConfigure.Visible = _step == 3;
        _stepFinish.Visible = _step == 4;

        _steps.Current = _step;
        _steps.Invalidate();

        _stepLabel.Text = $"Step {_step + 1} of 5 — " + _step switch
        {
            0 => "Welcome",
            1 => "Find VoiceAttack",
            2 => "Install files",
            3 => "Configure your bot",
            _ => "Done"
        };

        _back.Visible = _step > 0 && _step < 4;
        _back.Enabled = _back.Visible;
        _next.Text = _step == 4 ? "Close" : "Next >";

        if (_step == 2)
        {
            // Install on first visit — and again if the user went back and changed
            // the path, otherwise config.json would land in the stale folder.
            var target = Path.Combine(_vaPath.Text.Trim(), "Apps", PluginFolderName);
            if (_installLog.TextLength == 0 ||
                !string.Equals(_installDir, target, StringComparison.OrdinalIgnoreCase))
            {
                _installLog.Clear();
                RunInstall();
            }
        }
        if (_step == 4)
            _finishSummary.Text =
                $"Plugin installed to:\r\n{_installDir}\r\n\r\n" +
                "Two last things inside VoiceAttack:\r\n" +
                "1. Wrench icon → Options → General → enable Plugin Support.\r\n" +
                "2. Restart VoiceAttack, then check the log for:\r\n" +
                "   \"initialized. Bot token configured: True\".\r\n\r\n" +
                "Then create voice commands — see SETUP.md in the repo for examples.";

        // Keyboard users land on the first relevant control of each step.
        switch (_step)
        {
            case 0: _next.Focus(); break;
            case 1: _vaPath.Focus(); break;
            case 2: _next.Focus(); break;
            case 3: _token.Focus(); break;
            default: _next.Focus(); break;
        }
    }

    private void OnNext()
    {
        if (_step == 4) { Close(); return; }

        if (_step == 1)
        {
            var dir = _vaPath.Text.Trim();
            if (!Directory.Exists(dir) || !File.Exists(Path.Combine(dir, "VoiceAttack.exe")))
            {
                MessageBox.Show("That folder doesn't contain VoiceAttack.exe. Please check the path or use Browse.",
                    "Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        ShowStep(_step + 1);
    }

    // ---------- step indicator: five painted nodes with labels ----------

    private sealed class StepIndicator : Control
    {
        public int Current { get; set; }
        public Theme Theme { get; set; } = Dark;

        // Shared paint resources: StringFormat is not thread-safe for mutation,
        // but paints happen on the UI thread and these are never mutated.
        private static readonly StringFormat Centered = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        private static readonly StringFormat LabelFormat = new()
        {
            Alignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };

        public StepIndicator()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Surface);

            const int nodes = 5;
            float slot = (float)Width / nodes;
            float cy = 11f;
            const float r = 9f;
            using var font = new Font("Segoe UI", 8F);
            using var fontBold = new Font("Segoe UI", 8F, FontStyle.Bold);

            for (int i = 0; i < nodes; i++)
            {
                float cx = slot * i + slot / 2f;
                bool done = i < Current;
                bool current = i == Current;

                if (i > 0)
                {
                    float prevCx = slot * (i - 1) + slot / 2f;
                    using var line = new Pen(i <= Current ? Theme.Accent : Theme.Border, 2f);
                    g.DrawLine(line, prevCx + r + 2, cy, cx - r - 2, cy);
                }

                var rect = new RectangleF(cx - r, cy - r, r * 2, r * 2);
                if (done)
                {
                    using var fill = new SolidBrush(Theme.Accent);
                    g.FillEllipse(fill, rect);
                    using var white = new SolidBrush(Color.White);
                    using var checkFont = new Font("Segoe UI", 9F, FontStyle.Bold);
                    g.DrawString("✓", checkFont, white, new RectangleF(cx - r, cy - r - 1, r * 2, r * 2 + 2), Centered);
                }
                else if (current)
                {
                    using var ring = new Pen(Theme.Accent, 2.5f);
                    g.DrawEllipse(ring, rect);
                    using var numBrush = new SolidBrush(Theme.Accent);
                    using var numFont = new Font("Segoe UI", 8F, FontStyle.Bold);
                    g.DrawString((i + 1).ToString(), numFont, numBrush, new RectangleF(cx - r, cy - r, r * 2, r * 2), Centered);
                }
                else
                {
                    using var ring = new Pen(Theme.Border, 2f);
                    g.DrawEllipse(ring, rect);
                    using var numBrush = new SolidBrush(Theme.TextSecondary);
                    g.DrawString((i + 1).ToString(), font, numBrush, new RectangleF(cx - r, cy - r, r * 2, r * 2), Centered);
                }

                using var labelBrush = new SolidBrush(done || current ? Theme.TextPrimary : Theme.TextSecondary);
                g.DrawString(StepNames[i], done || current ? fontBold : font, labelBrush,
                    new RectangleF(slot * i, cy + r + 2, slot, 14), LabelFormat);
            }
        }
    }
}
