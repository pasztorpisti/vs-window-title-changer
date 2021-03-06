namespace VSWindowTitleChanger
{
	partial class TitleSetupEditor
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.labelWarnings = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.labelColumn = new System.Windows.Forms.Label();
            this.labelLine = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.panelExpressionEditor = new System.Windows.Forms.Panel();
            this.lineNumbers = new VSWindowTitleChanger.RichTextBoxLineNumbers();
            this.editTitleExpression = new VSWindowTitleChanger.ExpressionTextBox();
            this.buttonSave = new System.Windows.Forms.Button();
            this.buttonRevert = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.splitVariables = new System.Windows.Forms.SplitContainer();
            this.titleOrCompileError = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.listVariables = new System.Windows.Forms.ListView();
            this.columnVariableName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnVariableValue = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnVariableType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.buttonHelp = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonOK = new System.Windows.Forms.Button();
            this.checkBoxDebugExec = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.panelExpressionEditor.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitVariables)).BeginInit();
            this.splitVariables.Panel1.SuspendLayout();
            this.splitVariables.Panel2.SuspendLayout();
            this.splitVariables.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitMain
            // 
            this.splitMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitMain.Location = new System.Drawing.Point(12, 12);
            this.splitMain.Name = "splitMain";
            this.splitMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitMain.Panel1
            // 
            this.splitMain.Panel1.Controls.Add(this.labelWarnings);
            this.splitMain.Panel1.Controls.Add(this.label5);
            this.splitMain.Panel1.Controls.Add(this.labelColumn);
            this.splitMain.Panel1.Controls.Add(this.labelLine);
            this.splitMain.Panel1.Controls.Add(this.label4);
            this.splitMain.Panel1.Controls.Add(this.label3);
            this.splitMain.Panel1.Controls.Add(this.panelExpressionEditor);
            this.splitMain.Panel1.Controls.Add(this.buttonSave);
            this.splitMain.Panel1.Controls.Add(this.buttonRevert);
            this.splitMain.Panel1.Controls.Add(this.label1);
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.splitVariables);
            this.splitMain.Size = new System.Drawing.Size(1011, 618);
            this.splitMain.SplitterDistance = 205;
            this.splitMain.TabIndex = 0;
            // 
            // labelWarnings
            // 
            this.labelWarnings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelWarnings.AutoSize = true;
            this.labelWarnings.Location = new System.Drawing.Point(558, 6);
            this.labelWarnings.Name = "labelWarnings";
            this.labelWarnings.Size = new System.Drawing.Size(18, 20);
            this.labelWarnings.TabIndex = 14;
            this.labelWarnings.Text = "0";
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(335, 6);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(227, 20);
            this.label5.TabIndex = 13;
            this.label5.Text = "Warnings/unknown variables:";
            // 
            // labelColumn
            // 
            this.labelColumn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelColumn.AutoSize = true;
            this.labelColumn.Location = new System.Drawing.Point(726, 6);
            this.labelColumn.Name = "labelColumn";
            this.labelColumn.Size = new System.Drawing.Size(18, 20);
            this.labelColumn.TabIndex = 12;
            this.labelColumn.Text = "1";
            // 
            // labelLine
            // 
            this.labelLine.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelLine.AutoSize = true;
            this.labelLine.Location = new System.Drawing.Point(649, 6);
            this.labelLine.Name = "labelLine";
            this.labelLine.Size = new System.Drawing.Size(18, 20);
            this.labelLine.TabIndex = 11;
            this.labelLine.Text = "1";
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(607, 6);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(46, 20);
            this.label4.TabIndex = 10;
            this.label4.Text = "Line:";
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(691, 6);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(39, 20);
            this.label3.TabIndex = 9;
            this.label3.Text = "Col:";
            // 
            // panelExpressionEditor
            // 
            this.panelExpressionEditor.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelExpressionEditor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelExpressionEditor.Controls.Add(this.lineNumbers);
            this.panelExpressionEditor.Controls.Add(this.editTitleExpression);
            this.panelExpressionEditor.Location = new System.Drawing.Point(0, 40);
            this.panelExpressionEditor.Name = "panelExpressionEditor";
            this.panelExpressionEditor.Size = new System.Drawing.Size(1011, 163);
            this.panelExpressionEditor.TabIndex = 0;
            // 
            // lineNumbers
            // 
            this.lineNumbers.BackColor = System.Drawing.SystemColors.Control;
            this.lineNumbers.Dock = System.Windows.Forms.DockStyle.Left;
            this.lineNumbers.ForeColor = System.Drawing.Color.Gray;
            this.lineNumbers.Location = new System.Drawing.Point(0, 0);
            this.lineNumbers.Name = "lineNumbers";
            this.lineNumbers.RightLineColor = System.Drawing.Color.LightGray;
            this.lineNumbers.Size = new System.Drawing.Size(51, 161);
            this.lineNumbers.TabIndex = 2;
            this.lineNumbers.TextBox = this.editTitleExpression;
            // 
            // editTitleExpression
            // 
            this.editTitleExpression.AcceptsTab = true;
            this.editTitleExpression.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.editTitleExpression.AutoWordSelection = true;
            this.editTitleExpression.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.editTitleExpression.DetectUrls = false;
            this.editTitleExpression.Font = new System.Drawing.Font("Consolas", 9F);
            this.editTitleExpression.Location = new System.Drawing.Point(51, 0);
            this.editTitleExpression.MaxLength = 65536;
            this.editTitleExpression.Name = "editTitleExpression";
            this.editTitleExpression.Size = new System.Drawing.Size(958, 162);
            this.editTitleExpression.SmoothScroll = false;
            this.editTitleExpression.TabIndex = 1;
            this.editTitleExpression.Text = "";
            this.editTitleExpression.WordWrap = false;
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSave.Enabled = false;
            this.buttonSave.Location = new System.Drawing.Point(805, 0);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(100, 33);
            this.buttonSave.TabIndex = 1;
            this.buttonSave.Text = "Save";
            this.buttonSave.UseVisualStyleBackColor = true;
            // 
            // buttonRevert
            // 
            this.buttonRevert.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonRevert.Enabled = false;
            this.buttonRevert.Location = new System.Drawing.Point(911, 0);
            this.buttonRevert.Name = "buttonRevert";
            this.buttonRevert.Size = new System.Drawing.Size(100, 33);
            this.buttonRevert.TabIndex = 2;
            this.buttonRevert.Text = "Revert";
            this.buttonRevert.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 6);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(275, 20);
            this.label1.TabIndex = 0;
            this.label1.Text = "Window Title Expression: (F1=help)";
            // 
            // splitVariables
            // 
            this.splitVariables.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitVariables.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitVariables.Location = new System.Drawing.Point(0, 0);
            this.splitVariables.Name = "splitVariables";
            this.splitVariables.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitVariables.Panel1
            // 
            this.splitVariables.Panel1.Controls.Add(this.titleOrCompileError);
            // 
            // splitVariables.Panel2
            // 
            this.splitVariables.Panel2.Controls.Add(this.label6);
            this.splitVariables.Panel2.Controls.Add(this.label2);
            this.splitVariables.Panel2.Controls.Add(this.listVariables);
            this.splitVariables.Size = new System.Drawing.Size(1011, 409);
            this.splitVariables.SplitterDistance = 69;
            this.splitVariables.TabIndex = 0;
            // 
            // titleOrCompileError
            // 
            this.titleOrCompileError.Dock = System.Windows.Forms.DockStyle.Fill;
            this.titleOrCompileError.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.titleOrCompileError.Location = new System.Drawing.Point(0, 0);
            this.titleOrCompileError.Multiline = true;
            this.titleOrCompileError.Name = "titleOrCompileError";
            this.titleOrCompileError.ReadOnly = true;
            this.titleOrCompileError.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.titleOrCompileError.Size = new System.Drawing.Size(1011, 69);
            this.titleOrCompileError.TabIndex = 0;
            this.titleOrCompileError.Text = "Window Title";
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(639, 6);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(369, 20);
            this.label6.TabIndex = 4;
            this.label6.Text = "Double click or ENTER to paste variable names.";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(2, 6);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(263, 20);
            this.label2.TabIndex = 3;
            this.label2.Text = "Available constants and variables:";
            // 
            // listVariables
            // 
            this.listVariables.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listVariables.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnVariableName,
            this.columnVariableValue,
            this.columnVariableType});
            this.listVariables.FullRowSelect = true;
            this.listVariables.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listVariables.LabelWrap = false;
            this.listVariables.Location = new System.Drawing.Point(0, 34);
            this.listVariables.MultiSelect = false;
            this.listVariables.Name = "listVariables";
            this.listVariables.ShowItemToolTips = true;
            this.listVariables.Size = new System.Drawing.Size(1011, 302);
            this.listVariables.TabIndex = 0;
            this.listVariables.UseCompatibleStateImageBehavior = false;
            this.listVariables.View = System.Windows.Forms.View.Details;
            // 
            // columnVariableName
            // 
            this.columnVariableName.Text = "Variable";
            this.columnVariableName.Width = 200;
            // 
            // columnVariableValue
            // 
            this.columnVariableValue.Text = "Value";
            this.columnVariableValue.Width = 670;
            // 
            // columnVariableType
            // 
            this.columnVariableType.Text = "Type";
            this.columnVariableType.Width = 105;
            // 
            // buttonHelp
            // 
            this.buttonHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonHelp.Location = new System.Drawing.Point(12, 639);
            this.buttonHelp.Name = "buttonHelp";
            this.buttonHelp.Size = new System.Drawing.Size(100, 33);
            this.buttonHelp.TabIndex = 3;
            this.buttonHelp.Text = "Help";
            this.buttonHelp.UseVisualStyleBackColor = true;
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.Location = new System.Drawing.Point(923, 639);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(100, 33);
            this.buttonCancel.TabIndex = 2;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // buttonOK
            // 
            this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOK.Location = new System.Drawing.Point(817, 639);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(100, 33);
            this.buttonOK.TabIndex = 1;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            // 
            // checkBoxDebugExec
            // 
            this.checkBoxDebugExec.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.checkBoxDebugExec.AutoSize = true;
            this.checkBoxDebugExec.Location = new System.Drawing.Point(127, 644);
            this.checkBoxDebugExec.Name = "checkBoxDebugExec";
            this.checkBoxDebugExec.Size = new System.Drawing.Size(230, 24);
            this.checkBoxDebugExec.TabIndex = 4;
            this.checkBoxDebugExec.Text = "Debug the exec command";
            this.checkBoxDebugExec.UseVisualStyleBackColor = true;
            // 
            // TitleSetupEditor
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1035, 684);
            this.Controls.Add(this.checkBoxDebugExec);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonHelp);
            this.Controls.Add(this.splitMain);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "TitleSetupEditor";
            this.Text = "Visual Studio Title Setup";
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel1.PerformLayout();
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.panelExpressionEditor.ResumeLayout(false);
            this.splitVariables.Panel1.ResumeLayout(false);
            this.splitVariables.Panel1.PerformLayout();
            this.splitVariables.Panel2.ResumeLayout(false);
            this.splitVariables.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitVariables)).EndInit();
            this.splitVariables.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.SplitContainer splitMain;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.SplitContainer splitVariables;
		private System.Windows.Forms.TextBox titleOrCompileError;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.ListView listVariables;
		private System.Windows.Forms.ColumnHeader columnVariableName;
		private System.Windows.Forms.ColumnHeader columnVariableValue;
		private System.Windows.Forms.Button buttonRevert;
		private System.Windows.Forms.Button buttonHelp;
		private System.Windows.Forms.Button buttonCancel;
		private System.Windows.Forms.Button buttonOK;
		private System.Windows.Forms.Button buttonSave;
		private System.Windows.Forms.ColumnHeader columnVariableType;
		private System.Windows.Forms.Panel panelExpressionEditor;
		private ExpressionTextBox editTitleExpression;
		private RichTextBoxLineNumbers lineNumbers;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label labelColumn;
		private System.Windows.Forms.Label labelLine;
		private System.Windows.Forms.Label labelWarnings;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.CheckBox checkBoxDebugExec;


	}


}