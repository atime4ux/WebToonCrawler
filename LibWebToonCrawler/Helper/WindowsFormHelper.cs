namespace LibWebToonCrawler.Helper
{
	public class WindowsFormHelper
	{
		public void SetLabel(System.Windows.Forms.Label label, string text)
		{
			libMyUtil.clsThread.SetLabel(label, text);
		}

		public void buttonToggle(System.Windows.Forms.Button button, string text, bool enable)
		{
			libMyUtil.clsThread.buttonToggle(button, text, enable);
		}

		public void SetTextBox(System.Windows.Forms.TextBox textBox, string text, string appendTextYn)
		{
			libMyUtil.clsThread.SetTextBox(textBox, text, appendTextYn == "Y");
		}

		public delegate void SetTextBoxCallBack(System.Windows.Forms.TextBox textBox, string str, bool enable);
		public void SetTextBox(System.Windows.Forms.TextBox textBox, string str, bool enable)
		{
			if (textBox.InvokeRequired)
			{
				SetTextBoxCallBack dele = new SetTextBoxCallBack(SetTextBox);
				textBox.Invoke(dele, textBox, str, enable);
			}
			else
			{
				textBox.Enabled = enable;
				if (str.Length > 0)
				{
					textBox.AppendText(str);
				}
			}
		}

		public delegate void SetCheckBoxCallBack(System.Windows.Forms.CheckBox chkBox, bool enable);
		public void SetCheckBox(System.Windows.Forms.CheckBox chkBox, bool enable)
		{
			if (chkBox.InvokeRequired)
			{
				SetCheckBoxCallBack dele = new SetCheckBoxCallBack(SetCheckBox);
				chkBox.Invoke(dele, chkBox, enable);
			}
			else
			{
				chkBox.Enabled = enable;
			}
		}

		public delegate void SetRichTextBoxCallBack(System.Windows.Forms.RichTextBox textBox, string str, bool enable);
		public void SetRichTextBox(System.Windows.Forms.RichTextBox textBox, string str, bool enable)
		{
			if (textBox.InvokeRequired)
			{
				SetRichTextBoxCallBack dele = new SetRichTextBoxCallBack(SetRichTextBox);
				textBox.Invoke(dele, textBox, str, enable);
			}
			else
			{
				textBox.Enabled = enable;
				if (str.Length > 0)
				{
					textBox.AppendText(str);
				}
			}
		}

		public delegate string GetRichTextBoxValueCallBack(System.Windows.Forms.RichTextBox textBox);
		public string GetRichTextBoxValue(System.Windows.Forms.RichTextBox textBox)
		{
			string result = "";
			if (textBox.InvokeRequired)
			{
				GetRichTextBoxValueCallBack dele = new GetRichTextBoxValueCallBack(GetRichTextBoxValue);
				result = textBox.Invoke(dele, textBox).ToString();
			}
			else
			{
				result = textBox.Text;
			}

			return result;
		}
	}
}
