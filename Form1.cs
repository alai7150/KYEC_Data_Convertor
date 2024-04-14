using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO;
namespace Kyec_Data_Extractor
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        DataExtractor de = null;

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            listBoxTestList.Items.Clear();
            DialogResult dr = openFileDialog1.ShowDialog();
            if (dr == DialogResult.OK)
            {
                string fileName = openFileDialog1.FileName;
                if (File.Exists(fileName))
                {
                    de = new DataExtractor(fileName);
                    DataTable raw_data_table = de.ExtractData();
                    dataGridView1.DataSource = raw_data_table;

                    foreach (string k in de.TestDict.Keys)
                    {
                        listBoxTestList.Items.Add(k);
                    }
                }
            }
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            if(de != null)
            {
                DialogResult dr = saveFileDialog1.ShowDialog();
                if(dr == DialogResult.OK)
                {
                    try
                    {
                        string fileName = saveFileDialog1.FileName;
                        de.ExportCsv(fileName);
                        MessageBox.Show("Data exported to CSV.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch(Exception ee)
                    {
                        MessageBox.Show(ee.Message);
                    }
                }
            }
        }
    }
}
