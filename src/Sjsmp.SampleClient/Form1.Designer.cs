namespace Sjsmp.SampleClient
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.connectButton = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.propertyGrid1 = new System.Windows.Forms.PropertyGrid();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.refreshSchemaButton = new System.Windows.Forms.Button();
            this.refreshPropertiesButton = new System.Windows.Forms.Button();
            this.schemaTimer = new System.Windows.Forms.Timer(this.components);
            this.propertiesTimer = new System.Windows.Forms.Timer(this.components);
            this.settingsPropertyGrid = new System.Windows.Forms.PropertyGrid();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // connectButton
            // 
            this.connectButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.connectButton.Location = new System.Drawing.Point(627, 8);
            this.connectButton.Name = "connectButton";
            this.connectButton.Size = new System.Drawing.Size(101, 23);
            this.connectButton.TabIndex = 1;
            this.connectButton.Text = "Connect";
            this.connectButton.UseVisualStyleBackColor = true;
            this.connectButton.Click += new System.EventHandler(this.connectButton_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(13, 122);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.propertyGrid1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.richTextBox1);
            this.splitContainer1.Size = new System.Drawing.Size(715, 378);
            this.splitContainer1.SplitterDistance = 400;
            this.splitContainer1.TabIndex = 2;
            // 
            // propertyGrid1
            // 
            this.propertyGrid1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGrid1.Location = new System.Drawing.Point(0, 0);
            this.propertyGrid1.Name = "propertyGrid1";
            this.propertyGrid1.PropertySort = System.Windows.Forms.PropertySort.Categorized;
            this.propertyGrid1.Size = new System.Drawing.Size(400, 378);
            this.propertyGrid1.TabIndex = 0;
            // 
            // richTextBox1
            // 
            this.richTextBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBox1.Location = new System.Drawing.Point(0, 0);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.ReadOnly = true;
            this.richTextBox1.Size = new System.Drawing.Size(311, 378);
            this.richTextBox1.TabIndex = 0;
            this.richTextBox1.Text = "";
            // 
            // refreshSchemaButton
            // 
            this.refreshSchemaButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.refreshSchemaButton.Location = new System.Drawing.Point(13, 507);
            this.refreshSchemaButton.Name = "refreshSchemaButton";
            this.refreshSchemaButton.Size = new System.Drawing.Size(134, 27);
            this.refreshSchemaButton.TabIndex = 3;
            this.refreshSchemaButton.Text = "Refresh Schema";
            this.refreshSchemaButton.UseVisualStyleBackColor = true;
            this.refreshSchemaButton.Click += new System.EventHandler(this.refreshButton_Click);
            // 
            // refreshPropertiesButton
            // 
            this.refreshPropertiesButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.refreshPropertiesButton.Location = new System.Drawing.Point(153, 507);
            this.refreshPropertiesButton.Name = "refreshPropertiesButton";
            this.refreshPropertiesButton.Size = new System.Drawing.Size(134, 27);
            this.refreshPropertiesButton.TabIndex = 4;
            this.refreshPropertiesButton.Text = "Refresh Properties";
            this.refreshPropertiesButton.UseVisualStyleBackColor = true;
            this.refreshPropertiesButton.Click += new System.EventHandler(this.refreshPropertiesButton_Click);
            // 
            // schemaTimer
            // 
            this.schemaTimer.Interval = 10000;
            this.schemaTimer.Tick += new System.EventHandler(this.schemaTimer_Tick);
            // 
            // propertiesTimer
            // 
            this.propertiesTimer.Interval = 1000;
            this.propertiesTimer.Tick += new System.EventHandler(this.propertiesTimer_Tick);
            // 
            // settingsPropertyGrid
            // 
            this.settingsPropertyGrid.HelpVisible = false;
            this.settingsPropertyGrid.Location = new System.Drawing.Point(13, 8);
            this.settingsPropertyGrid.Name = "settingsPropertyGrid";
            this.settingsPropertyGrid.PropertySort = System.Windows.Forms.PropertySort.NoSort;
            this.settingsPropertyGrid.Size = new System.Drawing.Size(608, 108);
            this.settingsPropertyGrid.TabIndex = 5;
            this.settingsPropertyGrid.ToolbarVisible = false;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(740, 546);
            this.Controls.Add(this.settingsPropertyGrid);
            this.Controls.Add(this.refreshPropertiesButton);
            this.Controls.Add(this.refreshSchemaButton);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.connectButton);
            this.Name = "Form1";
            this.Text = "Form1";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button connectButton;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.PropertyGrid propertyGrid1;
        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.Button refreshSchemaButton;
        private System.Windows.Forms.Button refreshPropertiesButton;
        private System.Windows.Forms.Timer schemaTimer;
        private System.Windows.Forms.Timer propertiesTimer;
        private System.Windows.Forms.PropertyGrid settingsPropertyGrid;

    }
}

