using System;
using System.Drawing;
using System.Windows.Forms;
using UIKit;

namespace Aggregation.Dialogs;

public class TemplateEditorDialog : Form
{
	private readonly Controller _controller;

	private TextBox _textBox;

	private Label _labelHint;

	private RoundedButton _btnSave;

	private RoundedButton _btnCancel;

	private string _originalContent;

	public TemplateEditorDialog(Controller controller)
	{
		_controller = controller ?? throw new ArgumentNullException("controller");
		InitializeComponent();
		LoadTemplate();
	}

	private void LoadTemplate()
	{
		_originalContent = _controller.GetDefaultTemplate() ?? "";
		_textBox.Text = _originalContent;
		UpdateValidation();
	}

	private void UpdateValidation()
	{
		bool valid = Controller.IsTemplateValid(_textBox.Text);
		_labelHint.ForeColor = (valid ? Color.Gray : Color.OrangeRed);
		_labelHint.Text = (valid ? "Элемент {{CODE}} найден." : " Шаблон должен содержать {{CODE}}!");
		_btnSave.Enabled = valid;
	}

	private void OnSaveClick(object sender, EventArgs e)
	{
		if (!Controller.IsTemplateValid(_textBox.Text))
		{
			MessageBox.Show("Шаблон должен содержать {{CODE}}!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
		else if (_controller.SaveDefaultTemplate(_textBox.Text))
		{
			DialogResult = DialogResult.OK;
			Close();
		}
		else
		{
			MessageBox.Show("Ошибка сохранения шаблона.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void OnCancelClick(object sender, EventArgs e)
	{
		if (!(_textBox.Text != _originalContent) || MessageBox.Show("Отменить изменения?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}
	}

	private void OnTextChanged(object sender, EventArgs e)
	{
		UpdateValidation();
	}

	private void InitializeComponent()
	{
		base.SuspendLayout();
		this.Text = "Редактор шаблона";
		base.ClientSize = new System.Drawing.Size(700, 500);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		this.BackColor = System.Drawing.Color.FromArgb(45, 45, 45);
		System.Windows.Forms.Label labelHeader = new System.Windows.Forms.Label
		{
			Text = "Локальный шаблон печати (template.txt)",
			Location = new System.Drawing.Point(20, 15),
			Size = new System.Drawing.Size(660, 30),
			ForeColor = System.Drawing.Color.White,
			Font = new System.Drawing.Font("Tahoma", 14f, System.Drawing.FontStyle.Bold)
		};
		base.Controls.Add(labelHeader);
		this._textBox = new System.Windows.Forms.TextBox
		{
			Location = new System.Drawing.Point(20, 50),
			Size = new System.Drawing.Size(660, 320),
			Multiline = true,
			ScrollBars = System.Windows.Forms.ScrollBars.Both,
			Font = new System.Drawing.Font("Consolas", 12f),
			BackColor = System.Drawing.Color.Black,
			ForeColor = System.Drawing.Color.LightGreen,
			AcceptsReturn = true,
			AcceptsTab = true,
			WordWrap = false
		};
		this._textBox.TextChanged += new System.EventHandler(this.OnTextChanged);
		base.Controls.Add(this._textBox);
		this._labelHint = new System.Windows.Forms.Label
		{
			Location = new System.Drawing.Point(20, 380),
			Size = new System.Drawing.Size(660, 25),
			ForeColor = System.Drawing.Color.Gray,
			Font = new System.Drawing.Font("Tahoma", 11f)
		};
		base.Controls.Add(this._labelHint);
		System.Windows.Forms.Label labelInfo = new System.Windows.Forms.Label
		{
			Text = "Плейсхолдер: {{CODE}} — будет заменён на групповой код.",
			Location = new System.Drawing.Point(20, 405),
			Size = new System.Drawing.Size(660, 25),
			ForeColor = System.Drawing.Color.DarkGray,
			Font = new System.Drawing.Font("Tahoma", 10f)
		};
		base.Controls.Add(labelInfo);
		this._btnSave = new UIKit.RoundedButton
		{
			Text = "Сохранить",
			Location = new System.Drawing.Point(20, 440),
			Size = new System.Drawing.Size(320, 50),
			BackColor = System.Drawing.Color.FromArgb(0, 120, 0),
			ForeColor = System.Drawing.Color.White,
			Font = new System.Drawing.Font("Tahoma", 14f),
			CornerRadius = 12
		};
		this._btnSave.Click += new System.EventHandler(this.OnSaveClick);
		base.Controls.Add(this._btnSave);
		this._btnCancel = new UIKit.RoundedButton
		{
			Text = "Отмена",
			Location = new System.Drawing.Point(360, 440),
			Size = new System.Drawing.Size(320, 50),
			BackColor = System.Drawing.Color.FromArgb(60, 60, 60),
			ForeColor = System.Drawing.Color.White,
			Font = new System.Drawing.Font("Tahoma", 14f),
			CornerRadius = 12
		};
		this._btnCancel.Click += new System.EventHandler(this.OnCancelClick);
		base.Controls.Add(this._btnCancel);
		base.ResumeLayout(false);
	}
}
