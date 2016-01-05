using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace Sjsmp.SampleClient
{
    public sealed partial class Form1 : Form
    {
        private sealed class Properties
        {
            private readonly Form1 m_form;
            internal Properties(Form1 form)
            {
                m_form = form;
                connectionUrl = "http://localhost:40234/";
                user = "system";
            }

            public string connectionUrl { get; set; }
            public double schemaRefreshInterval 
            {
                get { return m_form.schemaTimer.Interval / 1000.0; }
                set { m_form.schemaTimer.Interval = (int)(value * 1000.0); }
            }
            public double propertiesRefreshInterval
            {
                get { return m_form.propertiesTimer.Interval / 1000.0; }
                set { m_form.propertiesTimer.Interval = (int)(value * 1000.0); }
            }

            public string user { get; set; }

            [PasswordPropertyText(true)]
            public string password { get; set; }
        }

        private readonly Properties m_properites;
        private SchemaAdapter m_adapter;

        public Form1()
        {
            InitializeComponent();
            m_properites = new Properties(this);
            settingsPropertyGrid.SelectedObject = m_properites;
        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            m_adapter = new SchemaAdapter(m_properites.connectionUrl, new ClientAuth(m_properites.user, m_properites.password));
            m_adapter.JsonReceivedEvent += (jObj) => { richTextBox1.Text = jObj.ToString(); };
            m_adapter.ActionResultEvent += (objectName, actionName, resultToken) => MessageBox.Show(resultToken != null? resultToken.ToString() : "null", objectName + "." + actionName + "()");
            //http://stackoverflow.com/a/10130126/376066 - need to do this manually (
            m_adapter.PropertyChanged += (pcSender, pcE) => propertyGrid1.Refresh() ;
            propertyGrid1.SelectedObject = m_adapter;
            m_adapter.RefreshSchema();
            m_adapter.RefreshPropertyValues();
            schemaTimer.Enabled = true;
            propertiesTimer.Enabled = true;
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            if (m_adapter != null)
            {
                m_adapter.RefreshSchema();
            }
        }

        private void refreshPropertiesButton_Click(object sender, EventArgs e)
        {
            if (m_adapter != null)
            {
                m_adapter.RefreshPropertyValues();
            }
        }

        private void schemaTimer_Tick(object sender, EventArgs e)
        {
            if (m_adapter != null)
            {
                m_adapter.RefreshSchema();
            }
        }

        private void propertiesTimer_Tick(object sender, EventArgs e)
        {
            if (m_adapter != null)
            {
                m_adapter.RefreshPropertyValues();
            }
        }

    }
}
