using System.Diagnostics;
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

    private readonly Label _title = new();
    private readonly Panel _body = new();
    private readonly Button _back = new();
    private readonly Button _next = new();
    private readonly Label _stepLabel = new();

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
    private readonly Label _finishSummary = new();
    private string? _preservedEncrypted;
    private string? _clientId;

    // Bot tokens are three dot-separated segments; mirrors the plugin's TokenValidator.
    private static readonly Regex TokenPattern = new(@"^[\w-]+\.[\w-]+\.[\w-]+$", RegexOptions.Compiled);
    private bool _tokenValid;

    private int _step;
    private string _installDir = "";

    public SetupForm()
    {
        Text = "VoiceAttack Discord Plugin — Setup";
        ClientSize = new Size(560, 470);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        _title.Font = new Font(Font.FontFamily, 14, FontStyle.Bold);
        _title.Text = "VoiceAttack Discord Plugin — Setup";
        _title.SetBounds(16, 12, 528, 28);
        Controls.Add(_title);

        _stepLabel.ForeColor = Color.Gray;
        _stepLabel.SetBounds(16, 42, 528, 20);
        Controls.Add(_stepLabel);

        _body.SetBounds(16, 66, 528, 330);
        Controls.Add(_body);

        _back.Text = "< Back";
        _back.SetBounds(16, 406, 100, 32);
        _back.Click += (_, _) => ShowStep(_step - 1);
        Controls.Add(_back);

        _next.Text = "Next >";
        _next.SetBounds(444, 406, 100, 32);
        _next.Click += (_, _) => OnNext();
        Controls.Add(_next);

        BuildWelcomeStep();
        BuildLocationStep();
        BuildInstallStep();
        BuildConfigureStep();
        BuildFinishStep();

        foreach (var p in new[] { _stepWelcome, _stepLocation, _stepInstall, _stepConfigure, _stepFinish })
        {
            p.Dock = DockStyle.Fill;
            p.Visible = false;
            _body.Controls.Add(p);
        }

        var detected = DetectVoiceAttack();
        if (detected != null) _vaPath.Text = detected;

        LoadExistingConfig();

        ShowStep(0);
    }

    // ---------- step 0: welcome ----------

    private void BuildWelcomeStep()
    {
        var info = new Label
        {
            Text = "Welcome! This wizard will:\r\n\r\n" +
                   "1. Find your VoiceAttack installation\r\n" +
                   "2. Install the plugin files (needs admin rights — that's the prompt you just saw)\r\n" +
                   "3. Help you configure your Discord bot\r\n\r\n" +
                   "You'll need a Discord bot token. Don't have one yet? No problem — " +
                   "the wizard will point you to the right page.\r\n\r\n" +
                   "Click Next to begin.",
            AutoSize = false,
            Dock = DockStyle.Fill
        };
        _stepWelcome.Controls.Add(info);
    }

    // ---------- step 1: locate VoiceAttack ----------

    private void BuildLocationStep()
    {
        var lbl = new Label { Text = "Where is VoiceAttack installed?", AutoSize = false };
        lbl.SetBounds(0, 8, 528, 24);
        _stepLocation.Controls.Add(lbl);

        _vaPath.SetBounds(0, 36, 420, 28);
        _stepLocation.Controls.Add(_vaPath);

        var browse = new Button { Text = "Browse…" };
        browse.SetBounds(428, 34, 100, 30);
        browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Select your VoiceAttack folder (contains VoiceAttack.exe)" };
            if (dlg.ShowDialog() == DialogResult.OK) _vaPath.Text = dlg.SelectedPath;
        };
        _stepLocation.Controls.Add(browse);

        var hint = new Label
        {
            Text = "Usually C:\\Program Files\\VoiceAttack or C:\\Program Files (x86)\\VoiceAttack.",
            ForeColor = Color.Gray,
            AutoSize = false
        };
        hint.SetBounds(0, 72, 528, 24);
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
        var lbl = new Label { Text = "Install the plugin files:", AutoSize = false };
        lbl.SetBounds(0, 8, 528, 24);
        _stepInstall.Controls.Add(lbl);

        _removeOld.Text = "Remove the old VA.DiscordVAPlugin folder if present (recommended)";
        _removeOld.Checked = true;
        _removeOld.AutoSize = false;
        _removeOld.SetBounds(0, 36, 528, 24);
        _stepInstall.Controls.Add(_removeOld);

        _installLog.Multiline = true;
        _installLog.ReadOnly = true;
        _installLog.ScrollBars = ScrollBars.Vertical;
        _installLog.SetBounds(0, 66, 528, 264);
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
        _stepConfigure.AutoScrollMinSize = new Size(508, 450);

        var lbl = new Label
        {
            Text = "Set up the Discord side. Do each step once, in order — most of it is " +
                   "copy-pasting between Discord's site and this box.",
            AutoSize = false
        };
        lbl.SetBounds(0, 8, 528, 40);
        _stepConfigure.Controls.Add(lbl);

        _instructions.Multiline = true;
        _instructions.ReadOnly = true;
        _instructions.ScrollBars = ScrollBars.Vertical;
        _instructions.WordWrap = true;
        _instructions.Text =
            "1. Click \"Open Discord Developer Portal\", then New Application → name it → Create.\r\n" +
            "2. Go to Bot → Reset Token → Copy. That's the bot token below.\r\n" +
            "3. Same Bot page → Privileged Gateway Intents → enable Server Members + Message Content → Save Changes.\r\n" +
            "4. Back here: click Validate token, then \"Invite bot to this server\" and pick your server.\r\n" +
            "5. In Discord, right-click your server icon → Copy Server ID, and paste it below. " +
            "(If it's greyed out: Settings → Advanced → turn on Developer Mode.)\r\n" +
            "6. Default channel is optional — that's where messages land (e.g. general).";
        _instructions.SetBounds(0, 52, 528, 112);
        _stepConfigure.Controls.Add(_instructions);

        var portal = new Button { Text = "Open Discord Developer Portal" };
        portal.SetBounds(0, 170, 250, 30);
        portal.Click += (_, _) => Process.Start(new ProcessStartInfo("https://discord.com/developers/applications") { UseShellExecute = true });
        _stepConfigure.Controls.Add(portal);

        _invite.Text = "Invite bot to this server";
        _invite.Enabled = false;
        _invite.SetBounds(258, 170, 250, 30);
        _invite.Click += (_, _) => InviteBot();
        _stepConfigure.Controls.Add(_invite);

        var tokenLbl = new Label { Text = "Bot token:", AutoSize = false };
        tokenLbl.SetBounds(0, 208, 528, 20);
        _stepConfigure.Controls.Add(tokenLbl);

        _token.UseSystemPasswordChar = true;
        _token.SetBounds(0, 230, 528, 28);
        _token.TextChanged += (_, _) =>
        {
            _tokenValid = false;
            _tokenStatus.Text = "";
            _clientId = null;
            _invite.Enabled = false;
            if (_token.Text.Trim().Length > 0) _preservedEncrypted = null;
            _save.Enabled = CanSave();
        };
        _stepConfigure.Controls.Add(_token);

        var validate = new Button { Text = "Validate token" };
        validate.SetBounds(0, 266, 140, 30);
        validate.Click += async (_, _) => await ValidateTokenAsync();
        _stepConfigure.Controls.Add(validate);

        _tokenStatus.AutoSize = false;
        _tokenStatus.SetBounds(150, 266, 378, 30);
        _stepConfigure.Controls.Add(_tokenStatus);

        var guildLbl = new Label { Text = "Server (guild) ID:", AutoSize = false };
        guildLbl.SetBounds(0, 304, 250, 20);
        _stepConfigure.Controls.Add(guildLbl);

        _guildId.SetBounds(0, 326, 250, 28);
        _stepConfigure.Controls.Add(_guildId);

        var channelLbl = new Label { Text = "Default channel (optional):", AutoSize = false };
        channelLbl.SetBounds(262, 304, 266, 20);
        _stepConfigure.Controls.Add(channelLbl);

        _channel.Text = "general";
        _channel.SetBounds(262, 326, 266, 28);
        _stepConfigure.Controls.Add(_channel);

        var save = _save;
        save.Text = "Save config.json";
        save.Enabled = false;
        save.SetBounds(0, 362, 140, 30);
        save.Click += (_, _) => SaveConfig();
        _stepConfigure.Controls.Add(save);

        var note = new Label
        {
            Text = "config.json stays only on this PC — the plugin encrypts your token on first run.",
            ForeColor = Color.Gray,
            AutoSize = false
        };
        note.SetBounds(150, 362, 378, 30);
        _stepConfigure.Controls.Add(note);
    }

    private async Task ValidateTokenAsync()
    {
        var token = _token.Text.Trim();
        if (token.Length == 0)
        {
            SetStatus("Paste your bot token first.", Color.Red);
            return;
        }

        if (!TokenPattern.IsMatch(token))
        {
            SetStatus("That doesn't look like a bot token (three parts separated by dots).", Color.Red);
            _tokenValid = false;
            _save.Enabled = false;
            return;
        }

        SetStatus("Checking with Discord…", Color.Gray);
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bot", token);
            using var res = await http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                SetStatus($"Invalid token (Discord returned {(int)res.StatusCode}).", Color.Red);
                _tokenValid = false;
                _save.Enabled = false;
                return;
            }
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var name = doc.RootElement.TryGetProperty("username", out var u) ? u.GetString() : "?";
            _clientId = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            _invite.Enabled = !string.IsNullOrEmpty(_clientId);
            SetStatus($"✓ Token valid — bot is \"{name}\".", Color.Green);
            _tokenValid = true;
            _save.Enabled = true;
        }
        catch (Exception ex)
        {
            SetStatus($"Could not reach Discord: {ex.Message}", Color.Red);
            _tokenValid = false;
            _save.Enabled = false;
        }
    }

    private void SetStatus(string text, Color color)
    {
        _tokenStatus.Text = text;
        _tokenStatus.ForeColor = color;
    }

    private void InviteBot()
    {
        if (string.IsNullOrEmpty(_clientId))
        {
            MessageBox.Show("Validate your bot token first, then invite the bot.", "Setup",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        long permissions = (1L << 11) | (1L << 16) | (1L << 20) | (1L << 21) | (1L << 22) | (1L << 23) | (1L << 24);
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
                ["AutoConnect"] = true,
                ["LogLevel"] = "Info"
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
                SetStatus("Token already configured & encrypted on this PC.", Color.Green);
                _save.Enabled = CanSave();
            }
            else if (root.TryGetProperty("BotToken", out var bot) && !string.IsNullOrWhiteSpace(bot.GetString()))
            {
                _token.Text = bot.GetString();
                _tokenValid = true;
                SetStatus("Loaded your saved token. Save to keep using it.", Color.Green);
                _save.Enabled = CanSave();
            }

            if (root.TryGetProperty("DefaultGuildId", out var g) && g.TryGetInt64(out var gid) && gid > 0)
                _guildId.Text = gid.ToString();
            if (root.TryGetProperty("DefaultChannelName", out var ch) && !string.IsNullOrWhiteSpace(ch.GetString()))
                _channel.Text = ch.GetString();
        }
        catch
        {
            // Not fatal — the wizard simply starts blank.
        }
    }

    // ---------- step 4: finish ----------

    private void BuildFinishStep()
    {
        _finishSummary.AutoSize = false;
        _finishSummary.Dock = DockStyle.Fill;
        _stepFinish.Controls.Add(_finishSummary);

        var openVa = new Button { Text = "Open VoiceAttack", Dock = DockStyle.Bottom, Height = 36 };
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

        _stepLabel.Text = $"Step {_step + 1} of 5 — " + _step switch
        {
            0 => "Welcome",
            1 => "Find VoiceAttack",
            2 => "Install files",
            3 => "Configure your bot",
            _ => "Done"
        };

        _back.Enabled = _step > 0 && _step < 4;
        _next.Text = _step == 4 ? "Close" : "Next >";

        if (_step == 2 && _installLog.TextLength == 0) RunInstall();
        if (_step == 4)
            _finishSummary.Text = "You're all set! 🎉\r\n\r\n" +
                                  $"Plugin installed to:\r\n{_installDir}\r\n\r\n" +
                                  "Two last things inside VoiceAttack:\r\n" +
                                  "1. Wrench icon → Options → General → enable Plugin Support.\r\n" +
                                  "2. Restart VoiceAttack, then check the log for:\r\n" +
                                  "   \"initialized. Bot token configured: True\".\r\n\r\n" +
                                  "Then create voice commands — see SETUP.md in the repo for examples.";
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
}
