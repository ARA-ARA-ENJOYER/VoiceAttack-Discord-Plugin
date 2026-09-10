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
    private readonly Label _tokenStatus = new();
    private readonly Button _save = new();
    private readonly Label _finishSummary = new();

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
        var lbl = new Label
        {
            Text = "Paste your Discord bot token and server (guild) ID.\r\n" +
                   "No bot yet? Click the button to open Discord's developer portal.",
            AutoSize = false
        };
        lbl.SetBounds(0, 8, 528, 44);
        _stepConfigure.Controls.Add(lbl);

        var portal = new Button { Text = "Open Discord Developer Portal" };
        portal.SetBounds(0, 56, 260, 30);
        portal.Click += (_, _) => Process.Start(new ProcessStartInfo("https://discord.com/developers/applications") { UseShellExecute = true });
        _stepConfigure.Controls.Add(portal);

        var tokenLbl = new Label { Text = "Bot token:", AutoSize = false };
        tokenLbl.SetBounds(0, 96, 528, 20);
        _stepConfigure.Controls.Add(tokenLbl);

        _token.UseSystemPasswordChar = true;
        _token.SetBounds(0, 118, 528, 28);
        _token.TextChanged += (_, _) =>
        {
            // Any edit invalidates the previous validation — the token on screen
            // is no longer the one Discord approved.
            _tokenValid = false;
            _save.Enabled = false;
            _tokenStatus.Text = "";
        };
        _stepConfigure.Controls.Add(_token);

        var guildLbl = new Label { Text = "Server (guild) ID:", AutoSize = false };
        guildLbl.SetBounds(0, 154, 528, 20);
        _stepConfigure.Controls.Add(guildLbl);

        _guildId.SetBounds(0, 176, 528, 28);
        _stepConfigure.Controls.Add(_guildId);

        var validate = new Button { Text = "Validate token" };
        validate.SetBounds(0, 212, 140, 30);
        validate.Click += async (_, _) => await ValidateTokenAsync();
        _stepConfigure.Controls.Add(validate);

        _tokenStatus.AutoSize = false;
        _tokenStatus.SetBounds(150, 212, 378, 30);
        _stepConfigure.Controls.Add(_tokenStatus);

        var save = _save;
        save.Text = "Save config.json";
        save.Enabled = false;
        save.SetBounds(0, 250, 140, 30);
        save.Click += (_, _) => SaveConfig();
        _stepConfigure.Controls.Add(save);
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

    private void SaveConfig()
    {
        if (!_tokenValid || !TokenPattern.IsMatch(_token.Text.Trim()))
        {
            MessageBox.Show("Click \"Validate token\" first — only a token Discord approved can be saved.", "Setup",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!ulong.TryParse(_guildId.Text.Trim(), out var guildId))
        {
            MessageBox.Show("Enter a numeric server ID first.", "Setup",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // Serialized (never string-concatenated) so any token text stays valid JSON.
            // The plugin encrypts BotToken in place on first load (DPAPI, this Windows user).
            var payload = new Dictionary<string, object?>
            {
                ["BotToken"] = _token.Text.Trim(),
                ["EncryptedBotToken"] = "",
                ["DefaultGuildId"] = guildId,
                ["DefaultChannelName"] = "general",
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
