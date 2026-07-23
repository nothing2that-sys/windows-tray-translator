using WindowsTrayTranslator.App;
using WindowsTrayTranslator.Translation;

namespace WindowsTrayTranslator.UI;

public sealed class SettingsForm : Form
{
    private readonly ApplicationController controller;
    private readonly TextBox apiKeyTextBox = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly ComboBox modelComboBox = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDown
    };
    private readonly Label savedKeyLabel = new() { AutoSize = true };
    private readonly Label statusLabel = new() { AutoSize = true, MaximumSize = new Size(420, 0) };
    private readonly Button testButton = new() { Text = "API 연결 테스트", AutoSize = true };

    public SettingsForm(ApplicationController controller)
    {
        this.controller = controller;
        Text = "Windows Tray Translator 설정";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 285);

        modelComboBox.Items.Add("gemini-3.5-flash");
        modelComboBox.Text = controller.Settings.Api.Model;
        RefreshSavedKeyLabel();

        TableLayoutPanel table = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 7
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        table.Controls.Add(new Label { Text = "API 공급자", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        table.Controls.Add(new Label { Text = "Gemini", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 0);
        table.Controls.Add(new Label { Text = "API 키", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        table.Controls.Add(apiKeyTextBox, 1, 1);
        table.Controls.Add(new Label { Text = "저장 상태", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        table.Controls.Add(savedKeyLabel, 1, 2);
        table.Controls.Add(new Label { Text = "모델", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        table.Controls.Add(modelComboBox, 1, 3);

        FlowLayoutPanel actions = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        Button saveButton = new() { Text = "저장", AutoSize = true };
        Button deleteButton = new() { Text = "저장된 API 키 삭제", AutoSize = true };
        Button closeButton = new() { Text = "닫기", AutoSize = true };
        saveButton.Click += (_, _) => SaveSettings();
        deleteButton.Click += (_, _) => DeleteApiKey();
        closeButton.Click += (_, _) => Close();
        testButton.Click += TestConnectionAsync;
        actions.Controls.Add(saveButton);
        actions.Controls.Add(testButton);
        actions.Controls.Add(deleteButton);
        actions.Controls.Add(closeButton);
        table.Controls.Add(actions, 1, 4);
        table.Controls.Add(statusLabel, 1, 5);

        Label phaseNotice = new()
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Text = "현재 설정 화면은 API 설정만 제공합니다. 언어와 단축키 UI는 Phase 6에서 추가됩니다."
        };
        table.Controls.Add(phaseNotice, 0, 6);
        table.SetColumnSpan(phaseNotice, 2);
        Controls.Add(table);
    }

    private void SaveSettings()
    {
        try
        {
            controller.SaveApiConfiguration(apiKeyTextBox.Text, modelComboBox.Text);
            apiKeyTextBox.Clear();
            RefreshSavedKeyLabel();
            SetStatus("API 설정을 저장했습니다.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
    }

    private async void TestConnectionAsync(object? sender, EventArgs e)
    {
        testButton.Enabled = false;
        SetStatus("연결을 확인하고 있습니다...", isError: false);
        try
        {
            if (!string.Equals(modelComboBox.Text.Trim(), controller.Settings.Api.Model, StringComparison.Ordinal))
            {
                SetStatus("연결 테스트 전에 모델 설정을 저장해 주세요.", isError: true);
                return;
            }

            TranslationResult result = await controller.TestConnectionAsync(apiKeyTextBox.Text, CancellationToken.None);
            SetStatus(result.IsSuccess ? "Gemini API 연결에 성공했습니다." : result.ErrorMessage!, !result.IsSuccess);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            testButton.Enabled = true;
        }
    }

    private void DeleteApiKey()
    {
        if (MessageBox.Show(
                "저장된 Gemini API 키를 삭제할까요?",
                "API 키 삭제",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            controller.DeleteApiKey();
            apiKeyTextBox.Clear();
            RefreshSavedKeyLabel();
            SetStatus("저장된 API 키를 삭제했습니다.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
    }

    private void RefreshSavedKeyLabel() =>
        savedKeyLabel.Text = controller.HasApiKey ? "저장됨 (DPAPI 보호)" : "저장되지 않음";

    private void SetStatus(string message, bool isError)
    {
        statusLabel.ForeColor = isError ? Color.Firebrick : Color.DarkGreen;
        statusLabel.Text = message;
    }
}
