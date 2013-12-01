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
			this.buttonSave = new System.Windows.Forms.Button();
			this.buttonRevert = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.splitVariables = new System.Windows.Forms.SplitContainer();
			this.titleOrCompileError = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.listVariables = new System.Windows.Forms.ListView();
			this.columnVariableName = new System.Windows.Forms.ColumnHeader();
			this.columnVariableValue = new System.Windows.Forms.ColumnHeader();
			this.columnVariableType = new System.Windows.Forms.ColumnHeader();
			this.buttonHelp = new System.Windows.Forms.Button();
			this.buttonCancel = new System.Windows.Forms.Button();
			this.buttonOK = new System.Windows.Forms.Button();
			this.editTitleExpression = new VSWindowTitleChanger.ColoredRichTextBox();
			this.splitMain.Panel1.SuspendLayout();
			this.splitMain.Panel2.SuspendLayout();
			this.splitMain.SuspendLayout();
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
			this.splitMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
			this.splitMain.Location = new System.Drawing.Point(12, 12);
			this.splitMain.Name = "splitMain";
			this.splitMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
			// 
			// splitMain.Panel1
			// 
			this.splitMain.Panel1.Controls.Add(this.buttonSave);
			this.splitMain.Panel1.Controls.Add(this.buttonRevert);
			this.splitMain.Panel1.Controls.Add(this.editTitleExpression);
			this.splitMain.Panel1.Controls.Add(this.label1);
			// 
			// splitMain.Panel2
			// 
			this.splitMain.Panel2.Controls.Add(this.splitVariables);
			this.splitMain.Size = new System.Drawing.Size(960, 606);
			this.splitMain.SplitterDistance = 193;
			this.splitMain.TabIndex = 17;
			// 
			// buttonSave
			// 
			this.buttonSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonSave.Enabled = false;
			this.buttonSave.Location = new System.Drawing.Point(804, 0);
			this.buttonSave.Name = "buttonSave";
			this.buttonSave.Size = new System.Drawing.Size(75, 20);
			this.buttonSave.TabIndex = 6;
			this.buttonSave.Text = "Save";
			this.buttonSave.UseVisualStyleBackColor = true;
			// 
			// buttonRevert
			// 
			this.buttonRevert.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonRevert.Enabled = false;
			this.buttonRevert.Location = new System.Drawing.Point(885, 0);
			this.buttonRevert.Name = "buttonRevert";
			this.buttonRevert.Size = new System.Drawing.Size(75, 20);
			this.buttonRevert.TabIndex = 7;
			this.buttonRevert.Text = "Revert";
			this.buttonRevert.UseVisualStyleBackColor = true;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(2, 5);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(260, 13);
			this.label1.TabIndex = 0;
			this.label1.Text = "Window Title Expression: (See help[F1] for examples.)";
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
			this.splitVariables.Panel2.Controls.Add(this.label2);
			this.splitVariables.Panel2.Controls.Add(this.listVariables);
			this.splitVariables.Size = new System.Drawing.Size(960, 409);
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
			this.titleOrCompileError.Size = new System.Drawing.Size(960, 69);
			this.titleOrCompileError.TabIndex = 1;
			this.titleOrCompileError.Text = "Window Title";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(2, 6);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(168, 13);
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
			this.listVariables.Location = new System.Drawing.Point(0, 23);
			this.listVariables.MultiSelect = false;
			this.listVariables.Name = "listVariables";
			this.listVariables.ShowItemToolTips = true;
			this.listVariables.Size = new System.Drawing.Size(960, 313);
			this.listVariables.TabIndex = 2;
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
			// 
			// buttonHelp
			// 
			this.buttonHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.buttonHelp.Location = new System.Drawing.Point(12, 627);
			this.buttonHelp.Name = "buttonHelp";
			this.buttonHelp.Size = new System.Drawing.Size(75, 23);
			this.buttonHelp.TabIndex = 5;
			this.buttonHelp.Text = "Help";
			this.buttonHelp.UseVisualStyleBackColor = true;
			// 
			// buttonCancel
			// 
			this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonCancel.Location = new System.Drawing.Point(897, 627);
			this.buttonCancel.Name = "buttonCancel";
			this.buttonCancel.Size = new System.Drawing.Size(75, 23);
			this.buttonCancel.TabIndex = 4;
			this.buttonCancel.Text = "Cancel";
			this.buttonCancel.UseVisualStyleBackColor = true;
			// 
			// buttonOK
			// 
			this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonOK.Location = new System.Drawing.Point(816, 627);
			this.buttonOK.Name = "buttonOK";
			this.buttonOK.Size = new System.Drawing.Size(75, 23);
			this.buttonOK.TabIndex = 3;
			this.buttonOK.Text = "OK";
			this.buttonOK.UseVisualStyleBackColor = true;
			// 
			// editTitleExpression
			// 
			this.editTitleExpression.AcceptsTab = true;
			this.editTitleExpression.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.editTitleExpression.DetectUrls = false;
			this.editTitleExpression.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.editTitleExpression.Location = new System.Drawing.Point(0, 22);
			this.editTitleExpression.MaxLength = 65535;
			this.editTitleExpression.Name = "editTitleExpression";
			this.editTitleExpression.Size = new System.Drawing.Size(960, 169);
			this.editTitleExpression.TabIndex = 0;
			this.editTitleExpression.TabStopChars = 16;
			this.editTitleExpression.Text = "";
			// 
			// TitleSetupEditor
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(984, 662);
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
			this.splitMain.ResumeLayout(false);
			this.splitVariables.Panel1.ResumeLayout(false);
			this.splitVariables.Panel1.PerformLayout();
			this.splitVariables.Panel2.ResumeLayout(false);
			this.splitVariables.Panel2.PerformLayout();
			this.splitVariables.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.SplitContainer splitMain;
		private ColoredRichTextBox editTitleExpression;
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


	}


}